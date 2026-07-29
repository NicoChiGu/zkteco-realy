using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ZktecoRelay.Models;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Devices;

internal sealed class DeviceSession : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _staThread;
    private readonly RealtimeEventHub _eventHub;
    private readonly IZktecoComClientFactory _clientFactory;
    private readonly TimeSpan _connectionProbeInterval;
    private readonly object _statusSync = new();
    private IZktecoComClient? _client;
    private bool _connected;
    private DateTimeOffset? _connectedAt;
    private string? _lastError;
    private DateTimeOffset? _lastCommunicationAt;
    private DateTimeOffset? _disconnectedAt;
    private int? _reconnectAttempt;
    private DateTimeOffset? _nextReconnectAt;
    private DateTimeOffset _nextConnectionProbeAt = DateTimeOffset.MaxValue;
    private bool _disposed;

    public DeviceSession(
        string deviceId,
        string ipAddress,
        int port,
        RealtimeEventHub eventHub,
        IZktecoComClientFactory clientFactory,
        TimeSpan? connectionProbeInterval = null)
    {
        _eventHub = eventHub;
        _clientFactory = clientFactory;
        _connectionProbeInterval =
            connectionProbeInterval ?? TimeSpan.FromSeconds(15);
        DeviceId = deviceId;
        IpAddress = ipAddress;
        Port = port;

        _staThread = new Thread(Run)
        {
            IsBackground = true,
            Name = $"zkteco-{deviceId}"
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
    }

    public string DeviceId { get; }
    public string IpAddress { get; }
    public int Port { get; }
    public bool Connected
    {
        get
        {
            lock (_statusSync)
            {
                return _connected;
            }
        }
    }

    public Task<DeviceConnectionResult> ConnectAsync(string password, CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            _client?.Dispose();
            _client = _clientFactory.Create(DeviceId, TryPublishRealtimeEvent);

            var connected = _client.Connect(IpAddress, Port, password);
            if (connected)
            {
                try
                {
                    _client.RegisterRealtimeEvents();
                }
                catch (Exception ex)
                {
                    TryPublishRealtimeEvent(new RealtimeEvent(
                        Guid.NewGuid().ToString("N"),
                        DeviceId,
                        "events_registration_failed",
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object?> { ["message"] = ex.Message }));
                }
            }
            int? vendorError = connected ? null : _client.GetLastError();
            if (connected)
            {
                MarkConnected();
            }
            else
            {
                MarkDisconnected(
                    $"Connect_Net failed. Vendor error: {vendorError}.",
                    vendorError,
                    "connect_failed");
            }

            return new DeviceConnectionResult(
                DeviceId,
                connected,
                GetStatus().LastError,
                vendorError);
        }, cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            _client?.Disconnect();
            MarkDisconnected(
                "The device was explicitly disconnected.",
                null,
                "explicit_disconnect");
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<AttendanceRecord>> ReadAttendanceAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            EnsureConnected();
            return _client!.ReadAttendance();
        }, cancellationToken);

    public Task<IReadOnlyList<AttendanceRecord>> ReadAttendanceAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        InvokeClientAsync(client => client.ReadAttendance(from, to), cancellationToken);

    public Task<OperationResult> ClearAttendanceAsync(
        AttendanceClearRequest request,
        CancellationToken cancellationToken) =>
        InvokeClientAsync(client => client.ClearAttendance(request), cancellationToken);

    public Task RestartAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            EnsureConnected();
            _client!.Restart();
            MarkDisconnected(
                "The device was restarted and must reconnect.",
                null,
                "device_restart");
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<UserInfo>> GetUsersAsync(CancellationToken ct) => InvokeClientAsync(c => c.GetUsers(), ct);
    public Task<UserInfo> GetUserAsync(string enrollNumber, CancellationToken ct) => InvokeClientAsync(c => c.GetUser(enrollNumber), ct);
    public Task<OperationResult> UpsertUserAsync(string enrollNumber, UpsertUserRequest request, CancellationToken ct) => InvokeClientAsync(c => c.UpsertUser(enrollNumber, request), ct);
    public Task<OperationResult> DeleteUserAsync(string enrollNumber, CancellationToken ct) => InvokeClientAsync(c => c.DeleteUser(enrollNumber), ct);
    public Task<FingerprintTemplateResult> GetFingerprintAsync(string enrollNumber, int fingerIndex, CancellationToken ct) => InvokeClientAsync(c => c.GetFingerprint(enrollNumber, fingerIndex), ct);
    public Task<OperationResult> SetFingerprintAsync(string enrollNumber, FingerprintTemplateRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetFingerprint(enrollNumber, request), ct);
    public Task<OperationResult> DeleteFingerprintAsync(string enrollNumber, int fingerIndex, CancellationToken ct) => InvokeClientAsync(c => c.DeleteFingerprint(enrollNumber, fingerIndex), ct);
    public Task<FaceTemplateResult> GetFaceAsync(string enrollNumber, int faceIndex, CancellationToken ct) => InvokeClientAsync(c => c.GetFace(enrollNumber, faceIndex), ct);
    public Task<OperationResult> SetFaceAsync(string enrollNumber, FaceTemplateRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetFace(enrollNumber, request), ct);
    public Task<OperationResult> DeleteFaceAsync(string enrollNumber, int faceIndex, CancellationToken ct) => InvokeClientAsync(c => c.DeleteFace(enrollNumber, faceIndex), ct);
    public Task<OperationResult> UploadUserPhotoAsync(string enrollNumber, UserPhotoRequest request, CancellationToken ct) => InvokeClientAsync(c => c.UploadUserPhoto(enrollNumber, request), ct);
    public Task<UserPhotoResult> DownloadUserPhotoAsync(string enrollNumber, CancellationToken ct) => InvokeClientAsync(c => c.DownloadUserPhoto(enrollNumber), ct);
    public Task<OperationResult> UnlockDoorAsync(DoorUnlockRequest request, CancellationToken ct) => InvokeClientAsync(c => c.UnlockDoor(request), ct);
    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct) => InvokeClientAsync(c => c.GetCapabilities(), ct);
    public Task<DoorStateResult> GetDoorStateAsync(CancellationToken ct) => InvokeClientAsync(c => c.GetDoorState(), ct);
    public Task<DoorModeResult> StartNormallyOpenAsync(CancellationToken ct) => InvokeClientAsync(c => c.StartNormallyOpen(), ct);
    public Task<DoorModeResult> EndNormallyOpenAsync(EndNormallyOpenRequest request, CancellationToken ct) => InvokeClientAsync(c => c.EndNormallyOpen(request), ct);
    public Task<TimeZoneInfoResult> GetTimeZoneAsync(int index, CancellationToken ct) => InvokeClientAsync(c => c.GetTimeZone(index), ct);
    public Task<OperationResult> SetTimeZoneAsync(TimeZoneRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetTimeZone(request), ct);
    public Task<AccessGroupInfo> GetAccessGroupAsync(int group, CancellationToken ct) => InvokeClientAsync(c => c.GetAccessGroup(group), ct);
    public Task<OperationResult> SetAccessGroupAsync(AccessGroupRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetAccessGroup(request), ct);
    public Task<UserAccessInfo> GetUserAccessAsync(string enrollNumber, CancellationToken ct) => InvokeClientAsync(c => c.GetUserAccess(enrollNumber), ct);
    public Task<OperationResult> SetUserAccessAsync(string enrollNumber, UserAccessRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetUserAccess(enrollNumber, request), ct);
    public Task<UnlockCombinationInfo> GetUnlockCombinationAsync(int number, CancellationToken ct) => InvokeClientAsync(c => c.GetUnlockCombination(number), ct);
    public Task<OperationResult> SetUnlockCombinationAsync(UnlockCombinationRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetUnlockCombination(request), ct);

    public DeviceStatus GetStatus() =>
        GetStatusSnapshot();

    public void UpdateReconnectState(
        int? attempt,
        DateTimeOffset? nextReconnectAt)
    {
        lock (_statusSync)
        {
            _reconnectAttempt = attempt;
            _nextReconnectAt = nextReconnectAt;
        }
    }

    public Task<bool> PingWorkerAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => true, cancellationToken);

    private Task<T> InvokeClientAsync<T>(Func<IZktecoComClient, T> operation, CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            EnsureConnected();
            var result = operation(_client!);
            RecordCommunication();
            return result;
        }, cancellationToken);

    private Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        _queue.Add(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                completion.TrySetResult(operation());
            }
            catch (Exception ex)
            {
                SetLastError(ex.Message);
                ProbeAfterCommandFailure(ex);
                completion.TrySetException(ex);
            }
        }, cancellationToken);

        return completion.Task;
    }

    private void Run()
    {
        while (!_queue.IsCompleted)
        {
            if (_queue.TryTake(out var operation, 25))
            {
                operation();
            }

            ProbeConnectionIfDue();
            PumpWindowsMessages();
        }

        while (_queue.TryTake(out var pending))
        {
            pending();
        }

        _client?.Dispose();
    }

    private static void PumpWindowsMessages()
    {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr HWnd;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint minFilter, uint maxFilter, uint removeMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    private void EnsureConnected()
    {
        if (!Connected || _client is null)
        {
            throw new DeviceUnavailableException($"Device '{DeviceId}' is not connected.");
        }
    }

    private DeviceStatus GetStatusSnapshot()
    {
        lock (_statusSync)
        {
            return new DeviceStatus(
                DeviceId,
                IpAddress,
                Port,
                _connected,
                _connectedAt,
                _lastError,
                _lastCommunicationAt,
                _disconnectedAt,
                _reconnectAttempt,
                _nextReconnectAt);
        }
    }

    private void MarkConnected()
    {
        var changed = false;
        var now = DateTimeOffset.UtcNow;
        lock (_statusSync)
        {
            changed = !_connected;
            _connected = true;
            _connectedAt = now;
            _lastCommunicationAt = now;
            _disconnectedAt = null;
            _lastError = null;
            _reconnectAttempt = null;
            _nextReconnectAt = null;
            _nextConnectionProbeAt = now.Add(_connectionProbeInterval);
        }

        if (changed)
        {
            PublishStatusEvent(true, "connected", null);
        }
    }

    private void MarkDisconnected(
        string error,
        int? vendorErrorCode,
        string reason)
    {
        var changed = false;
        var now = DateTimeOffset.UtcNow;
        lock (_statusSync)
        {
            changed = _connected;
            _connected = false;
            _connectedAt = null;
            _disconnectedAt ??= now;
            _lastError = error;
            _nextConnectionProbeAt = DateTimeOffset.MaxValue;
        }

        if (changed)
        {
            PublishStatusEvent(false, reason, vendorErrorCode);
        }
    }

    private void RecordCommunication()
    {
        lock (_statusSync)
        {
            _lastCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = null;
        }
    }

    private void SetLastError(string error)
    {
        lock (_statusSync)
        {
            _lastError = error;
        }
    }

    private void ProbeConnectionIfDue()
    {
        if (!Connected || DateTimeOffset.UtcNow < _nextConnectionProbeAt)
        {
            return;
        }

        ProbeConnection("periodic_probe");
    }

    private void ProbeAfterCommandFailure(Exception exception)
    {
        if (!Connected || _client is null ||
            exception is ArgumentException or CapabilityNotSupportedException)
        {
            return;
        }

        var probe = ProbeConnection("command_failure_probe");
        if (probe.Connected == true)
        {
            SetLastError(exception.Message);
        }
        if (probe.Connected is null &&
            exception is DeviceOperationException operationException &&
            IsSocketError(operationException.VendorErrorCode))
        {
            MarkDisconnected(
                operationException.Message,
                operationException.VendorErrorCode,
                "socket_error");
        }
    }

    private ConnectionProbeResult ProbeConnection(string reason)
    {
        if (_client is null)
        {
            return new ConnectionProbeResult(false, 0, "COM client is unavailable.");
        }

        var probe = _client.ProbeConnection();
        var now = DateTimeOffset.UtcNow;
        lock (_statusSync)
        {
            _nextConnectionProbeAt = now.Add(_connectionProbeInterval);
            if (probe.Connected == true)
            {
                _lastCommunicationAt = now;
                _lastError = null;
            }
            else if (probe.Connected is null && probe.Error is not null)
            {
                _lastError = probe.Error;
            }
        }

        if (probe.Connected == false)
        {
            MarkDisconnected(
                probe.Error ??
                    $"GetConnectStatus failed. Vendor error: {probe.VendorErrorCode}.",
                probe.VendorErrorCode,
                reason);
        }

        return probe;
    }

    private static bool IsSocketError(int? vendorErrorCode) =>
        vendorErrorCode is <= -10004 and >= -11000;

    private void PublishStatusEvent(
        bool connected,
        string reason,
        int? vendorErrorCode)
    {
        try
        {
            _eventHub.Publish(new RealtimeEvent(
                Guid.NewGuid().ToString("N"),
                DeviceId,
                "device_status_changed",
                DateTimeOffset.UtcNow,
                new Dictionary<string, object?>
                {
                    ["connected"] = connected,
                    ["reason"] = reason,
                    ["vendorErrorCode"] = vendorErrorCode
                }));
        }
        catch
        {
            // Event-store health is reported by readiness diagnostics. A
            // persistence failure must not crash the device STA worker.
        }
    }

    private void TryPublishRealtimeEvent(RealtimeEvent realtimeEvent)
    {
        try
        {
            _eventHub.Publish(realtimeEvent);
        }
        catch (Exception ex)
        {
            SetLastError(
                $"Realtime event persistence failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        _staThread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}
