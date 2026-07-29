using System.Globalization;
using ZktecoRelay.Models;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Devices;

internal sealed partial class ZktecoComClient : IZktecoComClient
{
    private const int MachineNumber = 1;
    private readonly dynamic _sdk;
    private readonly string _deviceId;
    private readonly Action<RealtimeEvent>? _eventSink;
    private bool _disposed;

    public ZktecoComClient(string deviceId, Action<RealtimeEvent>? eventSink = null)
    {
        _deviceId = deviceId;
        _eventSink = eventSink;
        var comType = Type.GetTypeFromProgID("zkemkeeper.ZKEM.1", throwOnError: false)
                      ?? Type.GetTypeFromProgID("zkemkeeper.ZKEM", throwOnError: false)
                      ?? throw new InvalidOperationException(
                          "ZKTeco COM component is not registered. Run the matching SDK registration script as Administrator.");

        _sdk = Activator.CreateInstance(comType)
               ?? throw new InvalidOperationException("Failed to create the ZKTeco COM object.");
    }

    public bool Connect(string ipAddress, int port, string communicationPassword)
    {
        ThrowIfDisposed();
        _sdk.SetCommPasswordEx(communicationPassword);
        return _sdk.Connect_Net(ipAddress, port);
    }

    public void Disconnect()
    {
        if (_disposed)
        {
            return;
        }

        _sdk.Disconnect();
    }

    public int GetLastError()
    {
        ThrowIfDisposed();
        var errorCode = 0;
        _sdk.GetLastError(ref errorCode);
        return errorCode;
    }

    public ConnectionProbeResult ProbeConnection()
    {
        ThrowIfDisposed();
        var errorCode = 0;
        try
        {
            var connected = _sdk.GetConnectStatus(out errorCode);
            return new ConnectionProbeResult(
                connected,
                errorCode,
                connected
                    ? null
                    : $"GetConnectStatus failed. Vendor error: {errorCode}.");
        }
        catch (Exception ex)
        {
            return new ConnectionProbeResult(
                null,
                errorCode,
                $"GetConnectStatus is unavailable or failed: {ex.Message}");
        }
    }

    public IReadOnlyList<AttendanceRecord> ReadAttendance(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        ThrowIfDisposed();
        var records = new List<AttendanceRecord>();
        var deviceDisabled = false;

        try
        {
            deviceDisabled = _sdk.EnableDevice(MachineNumber, false);
            var readSucceeded = false;
            if (from.HasValue && to.HasValue)
            {
                try
                {
                    readSucceeded = _sdk.ReadTimeGLogData(
                        MachineNumber,
                        FormatDeviceTime(from.Value),
                        FormatDeviceTime(to.Value));
                }
                catch
                {
                    // Older SDK registrations may not expose this method.
                }
            }
            else
            {
                readSucceeded = _sdk.ReadAllGLogData(MachineNumber);
            }

            if (!readSucceeded && from.HasValue && to.HasValue)
            {
                // ReadTimeGLogData is limited to newer firmware. Fall back to
                // reading the full buffer and filter locally for older devices.
                readSucceeded = _sdk.ReadAllGLogData(MachineNumber);
            }

            if (!readSucceeded)
            {
                throw new InvalidOperationException($"Reading attendance data failed. Vendor error: {GetLastError()}.");
            }

            while (true)
            {
                string enrollNumber = string.Empty;
                var verifyMode = 0;
                var inOutMode = 0;
                var year = 0;
                var month = 0;
                var day = 0;
                var hour = 0;
                var minute = 0;
                var second = 0;
                var workCode = 0;

                var hasRecord = _sdk.SSR_GetGeneralLogData(
                    MachineNumber,
                    out enrollNumber,
                    out verifyMode,
                    out inOutMode,
                    out year,
                    out month,
                    out day,
                    out hour,
                    out minute,
                    out second,
                    ref workCode);

                if (!hasRecord)
                {
                    break;
                }

                var localTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
                var timestamp = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
                if (from.HasValue && timestamp < from.Value ||
                    to.HasValue && timestamp > to.Value)
                {
                    continue;
                }

                records.Add(new AttendanceRecord(enrollNumber, verifyMode, inOutMode, timestamp, workCode));
            }

            return records;
        }
        finally
        {
            if (deviceDisabled)
            {
                _sdk.EnableDevice(MachineNumber, true);
            }
        }
    }

    public OperationResult ClearAttendance(AttendanceClearRequest request)
    {
        ThrowIfDisposed();
        var deviceDisabled = false;

        try
        {
            deviceDisabled = _sdk.EnableDevice(MachineNumber, false);

            var operation = request.Before.HasValue
                ? "DeleteAttlogByTime"
                : request.From.HasValue
                    ? "DeleteAttlogBetweenTheDate"
                    : "ClearGLog";

            bool succeeded;
            try
            {
                if (request.Before.HasValue)
                {
                    succeeded = _sdk.DeleteAttlogByTime(
                        MachineNumber,
                        FormatDeviceTime(request.Before.Value));
                }
                else if (request.From.HasValue && request.To.HasValue)
                {
                    succeeded = _sdk.DeleteAttlogBetweenTheDate(
                        MachineNumber,
                        FormatDeviceTime(request.From.Value),
                        FormatDeviceTime(request.To.Value));
                }
                else
                {
                    succeeded = _sdk.ClearGLog(MachineNumber);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult(false, null, $"{operation} is unavailable: {ex.Message}");
            }

            if (!succeeded)
            {
                return Failure(operation);
            }

            _sdk.RefreshData(MachineNumber);
            return new OperationResult(true);
        }
        finally
        {
            if (deviceDisabled)
            {
                _sdk.EnableDevice(MachineNumber, true);
            }
        }
    }

    public void Restart()
    {
        ThrowIfDisposed();
        if (!_sdk.RestartDevice(MachineNumber))
        {
            throw new InvalidOperationException($"RestartDevice failed. Vendor error: {GetLastError()}.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            UnregisterRealtimeEvents();
            _sdk.Disconnect();
        }
        catch
        {
            // Ignore shutdown errors from the vendor COM component.
        }

        try
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(_sdk))
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(_sdk);
            }
        }
        finally
        {
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static string FormatDeviceTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
