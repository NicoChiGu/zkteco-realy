using System.Collections.Concurrent;
using ZktecoRelay.Models;

namespace ZktecoRelay.Devices;

internal sealed class DeviceSession : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _staThread;
    private ZktecoComClient? _client;
    private bool _disposed;

    public DeviceSession(string deviceId, string ipAddress, int port)
    {
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
            _client = new ZktecoComClient();

            var connected = _client.Connect(IpAddress, Port, password);
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

    public DeviceStatus GetStatus() =>
        new(DeviceId, IpAddress, Port, Connected, ConnectedAt, LastError);

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
        foreach (var operation in _queue.GetConsumingEnumerable())
        {
            operation();
        }

        _client?.Dispose();
    }

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
