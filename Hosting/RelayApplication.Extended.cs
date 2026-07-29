using ZktecoRelay.Devices;
using ZktecoRelay.Models;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    private static void MapExtendedEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/devices/{deviceId}/users", (string deviceId, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetUsersAsync(deviceId, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/users/{enrollNumber}", (string deviceId, string enrollNumber, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetUserAsync(deviceId, enrollNumber, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/users/{enrollNumber}", (string deviceId, string enrollNumber, UpsertUserRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.UpsertUserAsync(deviceId, enrollNumber, request, ct)));

        app.MapDelete("/api/v1/devices/{deviceId}/users/{enrollNumber}", (string deviceId, string enrollNumber, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.DeleteUserAsync(deviceId, enrollNumber, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex:int}", (string deviceId, string enrollNumber, int fingerIndex, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetFingerprintAsync(deviceId, enrollNumber, fingerIndex, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex:int}", (string deviceId, string enrollNumber, int fingerIndex, FingerprintTemplateRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.SetFingerprintAsync(deviceId, enrollNumber, request with { FingerIndex = fingerIndex }, ct)));

        app.MapDelete("/api/v1/devices/{deviceId}/users/{enrollNumber}/fingerprints/{fingerIndex:int}", (string deviceId, string enrollNumber, int fingerIndex, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.DeleteFingerprintAsync(deviceId, enrollNumber, fingerIndex, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/users/{enrollNumber}/face", (string deviceId, string enrollNumber, int? faceIndex, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetFaceAsync(deviceId, enrollNumber, faceIndex ?? 50, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/users/{enrollNumber}/face", (string deviceId, string enrollNumber, FaceTemplateRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.SetFaceAsync(deviceId, enrollNumber, request, ct)));

        app.MapDelete("/api/v1/devices/{deviceId}/users/{enrollNumber}/face", (string deviceId, string enrollNumber, int? faceIndex, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.DeleteFaceAsync(deviceId, enrollNumber, faceIndex ?? 50, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/users/{enrollNumber}/photo", (string deviceId, string enrollNumber, UserPhotoRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.UploadUserPhotoAsync(deviceId, enrollNumber, request, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/users/{enrollNumber}/photo", (string deviceId, string enrollNumber, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.DownloadUserPhotoAsync(deviceId, enrollNumber, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/capabilities", (string deviceId, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetCapabilitiesAsync(deviceId, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/access/door-state", (string deviceId, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetDoorStateAsync(deviceId, ct)));

        app.MapPost("/api/v1/devices/{deviceId}/access/unlock", (string deviceId, DoorUnlockRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.UnlockDoorAsync(deviceId, request, ct)));

        app.MapPost("/api/v1/devices/{deviceId}/access/normally-open/start", (string deviceId, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.StartNormallyOpenAsync(deviceId, ct)));

        app.MapPost("/api/v1/devices/{deviceId}/access/normally-open/end", (string deviceId, EndNormallyOpenRequest request, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.EndNormallyOpenAsync(deviceId, request, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/access/time-zones/{index:int}", (string deviceId, int index, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetTimeZoneAsync(deviceId, index, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/access/time-zones/{index:int}", (string deviceId, int index, TimeZoneRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.SetTimeZoneAsync(deviceId, request with { TimeZoneIndex = index }, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/access/groups/{groupNumber:int}", (string deviceId, int groupNumber, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetAccessGroupAsync(deviceId, groupNumber, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/access/groups/{groupNumber:int}", (string deviceId, int groupNumber, AccessGroupRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.SetAccessGroupAsync(deviceId, request with { GroupNumber = groupNumber }, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/access/users/{enrollNumber}", (string deviceId, string enrollNumber, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetUserAccessAsync(deviceId, enrollNumber, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/access/users/{enrollNumber}", (string deviceId, string enrollNumber, UserAccessRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.SetUserAccessAsync(deviceId, enrollNumber, request, ct)));

        app.MapGet("/api/v1/devices/{deviceId}/access/unlock-combinations/{number:int}", (string deviceId, int number, DeviceManager manager, CancellationToken ct) =>
            Execute(() => manager.GetUnlockCombinationAsync(deviceId, number, ct)));

        app.MapPut("/api/v1/devices/{deviceId}/access/unlock-combinations/{number:int}", (string deviceId, int number, UnlockCombinationRequest request, DeviceManager manager, CancellationToken ct) =>
            ExecuteOperation(() => manager.SetUnlockCombinationAsync(deviceId, request with { CombinationNumber = number }, ct)));
    }

    private static async Task<IResult> Execute<T>(Func<Task<T>> operation)
    {
        try
        {
            return Results.Ok(await operation());
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new ApiError("device_not_found", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ApiError("invalid_request", ex.Message));
        }
        catch (DeviceUnavailableException ex)
        {
            return Results.Json(
                new ApiError("device_unavailable", ex.Message, ex.VendorErrorCode),
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (CapabilityNotSupportedException ex)
        {
            return Results.Json(
                new ApiError("capability_not_supported", ex.Message),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (DeviceOperationException ex)
        {
            return Results.Json(
                new ApiError("device_operation_failed", ex.Message, ex.VendorErrorCode),
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(
                new ApiError("device_operation_failed", ex.Message),
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (FormatException ex)
        {
            return Results.BadRequest(new ApiError("invalid_data", ex.Message));
        }
    }

    private static async Task<IResult> ExecuteOperation(Func<Task<OperationResult>> operation)
    {
        try
        {
            var result = await operation();
            return result.Success
                ? Results.Ok(result)
                : Results.Json(
                    new ApiError(
                        "device_operation_failed",
                        result.Message ?? "The vendor operation failed.",
                        result.VendorErrorCode),
                    statusCode: StatusCodes.Status502BadGateway);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new ApiError("device_not_found", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ApiError("invalid_request", ex.Message));
        }
        catch (DeviceUnavailableException ex)
        {
            return Results.Json(
                new ApiError("device_unavailable", ex.Message, ex.VendorErrorCode),
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (CapabilityNotSupportedException ex)
        {
            return Results.Json(
                new ApiError("capability_not_supported", ex.Message),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (DeviceOperationException ex)
        {
            return Results.Json(
                new ApiError("device_operation_failed", ex.Message, ex.VendorErrorCode),
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(
                new ApiError("device_operation_failed", ex.Message),
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (FormatException ex)
        {
            return Results.BadRequest(new ApiError("invalid_data", ex.Message));
        }
    }
}
