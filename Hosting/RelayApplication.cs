using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ZktecoRelay.Configuration;
using ZktecoRelay.Devices;
using ZktecoRelay.Models;
using ZktecoRelay.Realtime;
using ZktecoRelay.Persistence;
using ZktecoRelay.Diagnostics;

namespace ZktecoRelay.Hosting;

public sealed record RelayOverrides(
    string? BindUrl = null,
    string? ApiKey = null,
    string? AllowedNetworks = null,
    Action<string>? RequestLog = null);

public static partial class RelayApplication
{
    public static WebApplication Build(string[] args, RelayOverrides? overrides = null)
    {
        DotEnv.AutoLoad();

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<RelayDatabase>();
        builder.Services.AddSingleton<DeviceConfigurationStore>();
        builder.Services.AddSingleton<RealtimeEventStore>();
        builder.Services.AddSingleton<RealtimeEventHub>();
        builder.Services.AddSingleton<IZktecoComClientFactory, ZktecoComClientFactory>();
        builder.Services.AddSingleton<DeviceManager>();
        builder.Services.AddSingleton<RelayHealthService>();
        builder.Services.AddHostedService<DeviceAutoConnectService>();

        var bindUrl = overrides?.BindUrl
            ?? Environment.GetEnvironmentVariable("ZKTECO_BIND_URL")
            ?? "http://127.0.0.1:5080";

        var configuredApiKey = overrides?.ApiKey
            ?? Environment.GetEnvironmentVariable("ZKTECO_API_KEY");

        if (string.IsNullOrWhiteSpace(configuredApiKey) || configuredApiKey.Length < 16)
        {
            throw new InvalidOperationException("ZKTECO_API_KEY must be configured and contain at least 16 characters.");
        }

        var allowedNetworks = overrides?.AllowedNetworks
            ?? Environment.GetEnvironmentVariable("ZKTECO_ALLOWED_NETWORKS");
        var ipAccessPolicy = IpAccessPolicy.Parse(allowedNetworks);
        var requestLog = overrides?.RequestLog;

        builder.WebHost.UseUrls(bindUrl);
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            var startedAt = Stopwatch.GetTimestamp();
            var remoteAddress = context.Connection.RemoteIpAddress;
            var remoteText = remoteAddress?.ToString() ?? "unknown";

            try
            {
                var testServerRequest =
                    remoteAddress is null &&
                    app.Environment.IsEnvironment("Testing");
                if (!testServerRequest &&
                    !ipAccessPolicy.IsAllowed(remoteAddress))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new ApiError("ip_not_allowed", "The remote IP address is not allowed."));
                    return;
                }

                if (context.Request.Path.StartsWithSegments("/health") ||
                    context.Request.Path.StartsWithSegments("/docs") ||
                    context.Request.Path.StartsWithSegments("/openapi.yaml"))
                {
                    await next();
                    return;
                }

                var suppliedApiKey = context.Request.Headers.TryGetValue("X-API-Key", out var suppliedValues)
                    ? suppliedValues.ToString()
                    : context.Request.Path.StartsWithSegments("/api/v1/events/ws")
                        ? context.Request.Query["apiKey"].ToString()
                        : string.Empty;

                if (string.IsNullOrEmpty(suppliedApiKey))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new ApiError("missing_api_key", "X-API-Key header is required."));
                    return;
                }

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
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                try
                {
                    requestLog?.Invoke($"HTTP {context.Request.Method} {context.Request.Path} from {remoteText} -> {context.Response.StatusCode} ({elapsed:F0} ms)");
                }
                catch
                {
                    // Request logging must never affect API responses.
                }
            }
        });

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        MapHealthEndpoints(app);
        MapDocumentationEndpoints(app);
        MapProtocolEndpoints(app);
        MapDeviceEndpoints(app);
        MapAttendanceEndpoints(app);
        MapDeviceConfigurationEndpoints(app);
        MapExtendedEndpoints(app);
        MapRealtimeEndpoints(app);
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
                return result.Connected
                    ? Results.Ok(result)
                    : Results.Json(
                        new ApiError(
                            "device_unavailable",
                            result.Error ?? "The device connection failed.",
                            result.VendorErrorCode),
                        statusCode: StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiError("invalid_request", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(
                    new ApiError("device_operation_failed", ex.Message),
                    statusCode: StatusCodes.Status502BadGateway);
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
            catch (DeviceUnavailableException ex)
            {
                return Results.Json(
                    new ApiError(
                        "device_unavailable",
                        ex.Message,
                        ex.VendorErrorCode),
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (DeviceOperationException ex)
            {
                return Results.Json(
                    new ApiError(
                        "device_operation_failed",
                        ex.Message,
                        ex.VendorErrorCode),
                    statusCode: StatusCodes.Status502BadGateway);
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
            catch (DeviceUnavailableException ex)
            {
                return Results.Json(
                    new ApiError(
                        "device_unavailable",
                        ex.Message,
                        ex.VendorErrorCode),
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (DeviceOperationException ex)
            {
                return Results.Json(
                    new ApiError(
                        "device_operation_failed",
                        ex.Message,
                        ex.VendorErrorCode),
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }
}
