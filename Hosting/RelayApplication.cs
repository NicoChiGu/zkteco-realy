using System.Security.Cryptography;
using System.Text;
using ZktecoRelay.Configuration;
using ZktecoRelay.Devices;
using ZktecoRelay.Models;

namespace ZktecoRelay.Hosting;

public sealed record RelayOverrides(string? BindUrl = null, string? ApiKey = null);

public static class RelayApplication
{
    public static WebApplication Build(string[] args, RelayOverrides? overrides = null)
    {
        DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
        DotEnv.Load(Path.Combine(AppContext.BaseDirectory, ".env"));

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<DeviceManager>();
        builder.Services.AddHealthChecks();

        var bindUrl = overrides?.BindUrl
            ?? Environment.GetEnvironmentVariable("ZKTECO_BIND_URL")
            ?? "http://127.0.0.1:5080";

        var configuredApiKey = overrides?.ApiKey
            ?? Environment.GetEnvironmentVariable("ZKTECO_API_KEY");

        if (string.IsNullOrWhiteSpace(configuredApiKey) || configuredApiKey.Length < 16)
        {
            throw new InvalidOperationException("ZKTECO_API_KEY must be configured and contain at least 16 characters.");
        }

        builder.WebHost.UseUrls(bindUrl);
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await next();
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-API-Key", out var suppliedValues))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ApiError("missing_api_key", "X-API-Key header is required."));
                return;
            }

            var suppliedApiKey = suppliedValues.ToString();
            var expectedBytes = Encoding.UTF8.GetBytes(configuredApiKey);
            var suppliedBytes = Encoding.UTF8.GetBytes(suppliedApiKey);

            var valid = expectedBytes.Length == suppliedBytes.Length &&
                        CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);

            if (!valid)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ApiError("invalid_api_key", "The supplied API key is invalid."));
                return;
            }

            await next();
        });

        app.MapHealthChecks("/health");
        MapDeviceEndpoints(app);
        return app;
    }

    private static void MapDeviceEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/devices", (DeviceManager manager) => Results.Ok(manager.GetStatuses()));

        app.MapGet("/api/v1/devices/{deviceId}", (string deviceId, DeviceManager manager) =>
        {
            var status = manager.GetStatus(deviceId);
            return status is null
                ? Results.NotFound(new ApiError("device_not_found", "Device was not found."))
                : Results.Ok(status);
        });

        app.MapPost("/api/v1/devices/{deviceId}/connect", async (
            string deviceId,
            ConnectDeviceRequest request,
            DeviceManager manager,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await manager.ConnectAsync(deviceId, request, cancellationToken);
                return result.Connected ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiError("invalid_request", ex.Message));
            }
        });

        app.MapPost("/api/v1/devices/{deviceId}/disconnect", async (
            string deviceId,
            DeviceManager manager,
            CancellationToken cancellationToken) =>
        {
            var disconnected = await manager.DisconnectAsync(deviceId, cancellationToken);
            return disconnected
                ? Results.Ok(new { deviceId, connected = false })
                : Results.NotFound(new ApiError("device_not_found", "Device was not found."));
        });

        app.MapGet("/api/v1/devices/{deviceId}/attendance", async (
            string deviceId,
            DeviceManager manager,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await manager.ReadAttendanceAsync(deviceId, cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ApiError("device_not_found", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ApiError("device_unavailable", ex.Message));
            }
        });

        app.MapPost("/api/v1/devices/{deviceId}/restart", async (
            string deviceId,
            DeviceManager manager,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await manager.RestartAsync(deviceId, cancellationToken);
                return Results.Accepted(value: new { deviceId, restarted = true });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ApiError("device_not_found", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ApiError("device_unavailable", ex.Message));
            }
        });
    }
}
