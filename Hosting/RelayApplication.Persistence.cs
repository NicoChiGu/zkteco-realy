using ZktecoRelay.Devices;
using ZktecoRelay.Models;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    private static void MapDeviceConfigurationEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/device-configurations", (DeviceManager manager) =>
            Results.Ok(manager.GetConfigurations()));

        app.MapGet("/api/v1/device-configurations/{deviceId}", (string deviceId, DeviceManager manager) =>
        {
            var configuration = manager.GetConfiguration(deviceId);
            return configuration is null
                ? Results.NotFound(new ApiError("device_configuration_not_found", "Device configuration was not found."))
                : Results.Ok(configuration);
        });

        app.MapPut("/api/v1/device-configurations/{deviceId}", (
            string deviceId,
            UpdateDeviceConfigurationRequest request,
            DeviceManager manager) =>
        {
            try
            {
                manager.UpsertConfiguration(deviceId, request);
                return Results.Ok(manager.GetConfiguration(deviceId));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiError("invalid_request", ex.Message));
            }
        });

        app.MapDelete("/api/v1/device-configurations/{deviceId}", (string deviceId, DeviceManager manager) =>
            manager.DeleteConfiguration(deviceId)
                ? Results.NoContent()
                : Results.NotFound(new ApiError("device_configuration_not_found", "Device configuration was not found.")));
    }
}
