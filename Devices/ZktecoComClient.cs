using ZktecoRelay.Models;

namespace ZktecoRelay.Devices;

internal sealed partial class ZktecoComClient : IDisposable
{
    private const int MachineNumber = 1;
    private readonly dynamic _sdk;
    private bool _disposed;

    public ZktecoComClient()
    {
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

    public IReadOnlyList<AttendanceRecord> ReadAttendance()
    {
        ThrowIfDisposed();
        var records = new List<AttendanceRecord>();
        var deviceDisabled = false;

        try
        {
            deviceDisabled = _sdk.EnableDevice(MachineNumber, false);
            if (!_sdk.ReadAllGLogData(MachineNumber))
            {
                throw new InvalidOperationException($"ReadAllGLogData failed. Vendor error: {GetLastError()}.");
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
}
