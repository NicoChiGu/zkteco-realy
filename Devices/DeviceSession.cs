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
    private ZktecoComClient? _client;
    private bool _disposed;

    public DeviceSession(string deviceId, string ipAddress, int port, RealtimeEventHub eventHub)
    {
        _eventHub = eventHub;
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
    public bool Connected { get; private set; }
    public DateTimeOffset? ConnectedAt { get; private set; }
    public string? LastError { get; private set; }

    public Task<DeviceConnectionResult> ConnectAsync(string password, CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            _client?.Dispose();
            _client = new ZktecoComClient(DeviceId, _eventHub.Publish);

            var connected = _client.Connect(IpAddress, Port, password);
            if (connected)
            {
                try
                {
                    _client.RegisterRealtimeEvents();
                }
                catch (Exception ex)
                {
                    _eventHub.Publish(new RealtimeEvent(
                        Guid.NewGuid().ToString("N"),
                        DeviceId,
                        "events_registration_failed",
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object?> { ["message"] = ex.Message }));
                }
            }
            int? vendorError = connected ? null : _client.GetLastError();
            Connected = connected;
            ConnectedAt = connected ? DateTimeOffset.Now : null;
            LastError = connected ? null : $"Connect_Net failed. Vendor error: {vendorError}.";

            return new DeviceConnectionResult(DeviceId, connected, LastError, vendorError);
        }, cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            _client?.Disconnect();
            Connected = false;
            ConnectedAt = null;
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<AttendanceRecord>> ReadAttendanceAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            EnsureConnected();
            return _client!.ReadAttendance();
        }, cancellationToken);

    public Task RestartAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            EnsureConnected();
            _client!.Restart();
            Connected = false;
            ConnectedAt = null;
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
    public Task<OperationResult> UnlockDoorAsync(DoorUnlockRequest request, CancellationToken ct) => InvokeClientAsync(c => c.UnlockDoor(request), ct);
    public Task<TimeZoneInfoResult> GetTimeZoneAsync(int index, CancellationToken ct) => InvokeClientAsync(c => c.GetTimeZone(index), ct);
    public Task<OperationResult> SetTimeZoneAsync(TimeZoneRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetTimeZone(request), ct);
    public Task<AccessGroupInfo> GetAccessGroupAsync(int group, CancellationToken ct) => InvokeClientAsync(c => c.GetAccessGroup(group), ct);
    public Task<OperationResult> SetAccessGroupAsync(AccessGroupRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetAccessGroup(request), ct);
    public Task<UserAccessInfo> GetUserAccessAsync(string enrollNumber, CancellationToken ct) => InvokeClientAsync(c => c.GetUserAccess(enrollNumber), ct);
    public Task<OperationResult> SetUserAccessAsync(string enrollNumber, UserAccessRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetUserAccess(enrollNumber, request), ct);
    public Task<UnlockCombinationInfo> GetUnlockCombinationAsync(int number, CancellationToken ct) => InvokeClientAsync(c => c.GetUnlockCombination(number), ct);
    public Task<OperationResult> SetUnlockCombinationAsync(UnlockCombinationRequest request, CancellationToken ct) => InvokeClientAsync(c => c.SetUnlockCombination(request), ct);

    public DeviceStatus GetStatus() =>
        new(DeviceId, IpAddress, Port, Connected, ConnectedAt, LastError);

    private Task<T> InvokeClientAsync<T>(Func<ZktecoComClient, T> operation, CancellationToken cancellationToken) =>
        InvokeAsync(() =>
        {
            EnsureConnected();
            return operation(_client!);
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
                LastError = ex.Message;
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
            throw new InvalidOperationException($"Device '{DeviceId}' is not connected.");
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
