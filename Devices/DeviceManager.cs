using System.Collections.Concurrent;
using System.Net;
using ZktecoRelay.Models;

namespace ZktecoRelay.Devices;

public sealed class DeviceManager : IDisposable
{
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<DeviceStatus> GetStatuses() =>
        _sessions.Values.Select(session => session.GetStatus()).OrderBy(status => status.DeviceId).ToArray();

    public DeviceStatus? GetStatus(string deviceId) =>
        _sessions.TryGetValue(deviceId, out var session) ? session.GetStatus() : null;

    public async Task<DeviceConnectionResult> ConnectAsync(
        string deviceId,
        ConnectDeviceRequest request,
        CancellationToken cancellationToken)
    {
        Validate(deviceId, request);

        if (_sessions.TryGetValue(deviceId, out var existing))
        {
            if (!string.Equals(existing.IpAddress, request.IpAddress, StringComparison.OrdinalIgnoreCase) ||
                existing.Port != request.Port)
            {
                await existing.DisconnectAsync(cancellationToken);
                existing.Dispose();
                _sessions.TryRemove(deviceId, out _);
            }
        }

        var session = _sessions.GetOrAdd(
            deviceId,
            _ => new DeviceSession(deviceId, request.IpAddress, request.Port));

        return await session.ConnectAsync(request.CommunicationPassword ?? string.Empty, cancellationToken);
    }

    public async Task<bool> DisconnectAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove(deviceId, out var session))
        {
            return false;
        }

        try
        {
            await session.DisconnectAsync(cancellationToken);
        }
        finally
        {
            session.Dispose();
        }

        return true;
    }

    public Task<IReadOnlyList<AttendanceRecord>> ReadAttendanceAsync(
        string deviceId,
        CancellationToken cancellationToken) =>
        GetRequiredSession(deviceId).ReadAttendanceAsync(cancellationToken);

    public Task RestartAsync(string deviceId, CancellationToken cancellationToken) =>
        GetRequiredSession(deviceId).RestartAsync(cancellationToken);

    private DeviceSession GetRequiredSession(string deviceId)
    {
        if (!_sessions.TryGetValue(deviceId, out var session))
        {
            throw new KeyNotFoundException($"Device '{deviceId}' was not found.");
        }

        return session;
    }

    private static void Validate(string deviceId, ConnectDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 64)
        {
            throw new ArgumentException("deviceId is required and must not exceed 64 characters.");
        }

        if (!IPAddress.TryParse(request.IpAddress, out _))
        {
            throw new ArgumentException("IpAddress must be a valid IPv4 or IPv6 address.");
        }

        if (request.Port is < 1 or > 65535)
        {
            throw new ArgumentException("Port must be between 1 and 65535.");
        }
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
    }
}
