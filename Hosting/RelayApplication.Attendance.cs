using ZktecoRelay.Devices;
using ZktecoRelay.Models;

namespace ZktecoRelay.Hosting;

public static partial class RelayApplication
{
    private static void MapAttendanceEndpoints(WebApplication app)
    {
        app.MapGet("/api/v1/devices/{deviceId}/attendance/query", (
            string deviceId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            DeviceManager manager,
            CancellationToken cancellationToken) =>
            Execute(() => manager.QueryAttendanceAsync(
                deviceId,
                from,
                to,
                page ?? 1,
                pageSize ?? 100,
                cancellationToken)));

        app.MapPost("/api/v1/devices/{deviceId}/attendance/clear", (
            string deviceId,
            AttendanceClearRequest request,
            DeviceManager manager,
            CancellationToken cancellationToken) =>
            ExecuteOperation(() => manager.ClearAttendanceAsync(
                deviceId,
                request,
                cancellationToken)));
    }
}
