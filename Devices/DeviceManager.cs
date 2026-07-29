using System.Collections.Concurrent;
using System.Net;
using ZktecoRelay.Models;
using ZktecoRelay.Persistence;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Devices;

public sealed class DeviceManager : IDisposable
{
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly DeviceConfigurationStore _configurationStore;
    private readonly RealtimeEventHub _eventHub;
    private readonly IZktecoComClientFactory _clientFactory;

    public DeviceManager(
        DeviceConfigurationStore configurationStore,
        RealtimeEventHub eventHub,
        IZktecoComClientFactory clientFactory)
    {
        _configurationStore = configurationStore;
        _eventHub = eventHub;
        _clientFactory = clientFactory;
    }

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
        _configurationStore.Upsert(
            deviceId,
            request.IpAddress,
            request.Port,
            request.CommunicationPassword ?? string.Empty,
            autoConnect: true);

        return await ConnectCoreAsync(deviceId, request, cancellationToken);
    }

    public Task<DeviceConnectionResult> RestoreAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken) =>
        ConnectCoreAsync(
            configuration.DeviceId,
            new ConnectDeviceRequest(configuration.IpAddress, configuration.Port, configuration.CommunicationPassword),
            cancellationToken);

    private async Task<DeviceConnectionResult> ConnectCoreAsync(
        string deviceId,
        ConnectDeviceRequest request,
        CancellationToken cancellationToken)
    {
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
            _ => new DeviceSession(
                deviceId,
                request.IpAddress,
                request.Port,
                _eventHub,
                _clientFactory));

        return await session.ConnectAsync(request.CommunicationPassword ?? string.Empty, cancellationToken);
    }

    public async Task<bool> DisconnectAsync(string deviceId, CancellationToken cancellationToken)
    {
        _configurationStore.SetAutoConnect(deviceId, enabled: false);

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

    public IReadOnlyList<DeviceConfigurationView> GetConfigurations() => _configurationStore.GetViews();

    public DeviceConfigurationView? GetConfiguration(string deviceId) =>
        _configurationStore.GetViews().FirstOrDefault(configuration =>
            string.Equals(configuration.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

    public void UpsertConfiguration(string deviceId, UpdateDeviceConfigurationRequest request)
    {
        Validate(deviceId, new ConnectDeviceRequest(request.IpAddress, request.Port, request.CommunicationPassword));
        var existing = _configurationStore.Get(deviceId);
        var password = request.CommunicationPassword ?? existing?.CommunicationPassword ?? string.Empty;
        _configurationStore.Upsert(deviceId, request.IpAddress, request.Port, password, request.AutoConnect);
    }

    public bool DeleteConfiguration(string deviceId) => _configurationStore.Delete(deviceId);

    public Task<IReadOnlyList<AttendanceRecord>> ReadAttendanceAsync(
        string deviceId,
        CancellationToken cancellationToken) =>
        GetRequiredSession(deviceId).ReadAttendanceAsync(cancellationToken);

    public async Task<AttendancePage> QueryAttendanceAsync(
        string deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidateAttendanceQuery(from, to, page, pageSize);
        var records = await GetRequiredSession(deviceId).ReadAttendanceAsync(from, to, cancellationToken);
        var totalItems = records.Count;
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= totalItems
            ? Array.Empty<AttendanceRecord>()
            : records
                .Skip((int)skip)
                .Take(pageSize)
                .ToArray();

        return new AttendancePage(items, page, pageSize, totalItems, totalPages, from, to);
    }

    public Task<OperationResult> ClearAttendanceAsync(
        string deviceId,
        AttendanceClearRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAttendanceClear(request);
        return GetRequiredSession(deviceId).ClearAttendanceAsync(request, cancellationToken);
    }

    public Task RestartAsync(string deviceId, CancellationToken cancellationToken) =>
        GetRequiredSession(deviceId).RestartAsync(cancellationToken);

    public Task<IReadOnlyList<UserInfo>> GetUsersAsync(string deviceId, CancellationToken ct) => GetRequiredSession(deviceId).GetUsersAsync(ct);
    public Task<UserInfo> GetUserAsync(string deviceId, string enrollNumber, CancellationToken ct) => GetRequiredSession(deviceId).GetUserAsync(enrollNumber, ct);
    public Task<OperationResult> UpsertUserAsync(string deviceId, string enrollNumber, UpsertUserRequest request, CancellationToken ct) => GetRequiredSession(deviceId).UpsertUserAsync(enrollNumber, request, ct);
    public Task<OperationResult> DeleteUserAsync(string deviceId, string enrollNumber, CancellationToken ct) => GetRequiredSession(deviceId).DeleteUserAsync(enrollNumber, ct);
    public Task<FingerprintTemplateResult> GetFingerprintAsync(string deviceId, string enrollNumber, int fingerIndex, CancellationToken ct) => GetRequiredSession(deviceId).GetFingerprintAsync(enrollNumber, fingerIndex, ct);
    public Task<OperationResult> SetFingerprintAsync(string deviceId, string enrollNumber, FingerprintTemplateRequest request, CancellationToken ct) => GetRequiredSession(deviceId).SetFingerprintAsync(enrollNumber, request, ct);
    public Task<OperationResult> DeleteFingerprintAsync(string deviceId, string enrollNumber, int fingerIndex, CancellationToken ct) => GetRequiredSession(deviceId).DeleteFingerprintAsync(enrollNumber, fingerIndex, ct);
    public Task<FaceTemplateResult> GetFaceAsync(string deviceId, string enrollNumber, int faceIndex, CancellationToken ct) => GetRequiredSession(deviceId).GetFaceAsync(enrollNumber, faceIndex, ct);
    public Task<OperationResult> SetFaceAsync(string deviceId, string enrollNumber, FaceTemplateRequest request, CancellationToken ct) => GetRequiredSession(deviceId).SetFaceAsync(enrollNumber, request, ct);
    public Task<OperationResult> DeleteFaceAsync(string deviceId, string enrollNumber, int faceIndex, CancellationToken ct) => GetRequiredSession(deviceId).DeleteFaceAsync(enrollNumber, faceIndex, ct);
    public Task<OperationResult> UploadUserPhotoAsync(string deviceId, string enrollNumber, UserPhotoRequest request, CancellationToken ct) => GetRequiredSession(deviceId).UploadUserPhotoAsync(enrollNumber, request, ct);
    public Task<UserPhotoResult> DownloadUserPhotoAsync(string deviceId, string enrollNumber, CancellationToken ct) => GetRequiredSession(deviceId).DownloadUserPhotoAsync(enrollNumber, ct);
    public Task<OperationResult> UnlockDoorAsync(string deviceId, DoorUnlockRequest request, CancellationToken ct) => GetRequiredSession(deviceId).UnlockDoorAsync(request, ct);
    public Task<DeviceCapabilities> GetCapabilitiesAsync(string deviceId, CancellationToken ct) => GetRequiredSession(deviceId).GetCapabilitiesAsync(ct);
    public Task<DoorStateResult> GetDoorStateAsync(string deviceId, CancellationToken ct) => GetRequiredSession(deviceId).GetDoorStateAsync(ct);
    public Task<DoorModeResult> StartNormallyOpenAsync(string deviceId, CancellationToken ct) => GetRequiredSession(deviceId).StartNormallyOpenAsync(ct);
    public Task<DoorModeResult> EndNormallyOpenAsync(string deviceId, EndNormallyOpenRequest request, CancellationToken ct) => GetRequiredSession(deviceId).EndNormallyOpenAsync(request, ct);
    public Task<TimeZoneInfoResult> GetTimeZoneAsync(string deviceId, int index, CancellationToken ct) => GetRequiredSession(deviceId).GetTimeZoneAsync(index, ct);
    public Task<OperationResult> SetTimeZoneAsync(string deviceId, TimeZoneRequest request, CancellationToken ct) => GetRequiredSession(deviceId).SetTimeZoneAsync(request, ct);
    public Task<AccessGroupInfo> GetAccessGroupAsync(string deviceId, int group, CancellationToken ct) => GetRequiredSession(deviceId).GetAccessGroupAsync(group, ct);
    public Task<OperationResult> SetAccessGroupAsync(string deviceId, AccessGroupRequest request, CancellationToken ct) => GetRequiredSession(deviceId).SetAccessGroupAsync(request, ct);
    public Task<UserAccessInfo> GetUserAccessAsync(string deviceId, string enrollNumber, CancellationToken ct) => GetRequiredSession(deviceId).GetUserAccessAsync(enrollNumber, ct);
    public Task<OperationResult> SetUserAccessAsync(string deviceId, string enrollNumber, UserAccessRequest request, CancellationToken ct) => GetRequiredSession(deviceId).SetUserAccessAsync(enrollNumber, request, ct);
    public Task<UnlockCombinationInfo> GetUnlockCombinationAsync(string deviceId, int number, CancellationToken ct) => GetRequiredSession(deviceId).GetUnlockCombinationAsync(number, ct);
    public Task<OperationResult> SetUnlockCombinationAsync(string deviceId, UnlockCombinationRequest request, CancellationToken ct) => GetRequiredSession(deviceId).SetUnlockCombinationAsync(request, ct);

    public void UpdateReconnectState(
        string deviceId,
        int? attempt,
        DateTimeOffset? nextReconnectAt)
    {
        if (_sessions.TryGetValue(deviceId, out var session))
        {
            session.UpdateReconnectState(attempt, nextReconnectAt);
        }
    }

    public async Task<bool> AreSessionWorkersResponsiveAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                await session
                    .PingWorkerAsync(cancellationToken)
                    .WaitAsync(timeout, cancellationToken);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

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

    private static void ValidateAttendanceQuery(
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentException("page must be greater than or equal to 1.");
        }

        if (pageSize is < 1 or > 1000)
        {
            throw new ArgumentException("pageSize must be between 1 and 1000.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("from must be earlier than or equal to to.");
        }
    }

    private static void ValidateAttendanceClear(AttendanceClearRequest request)
    {
        if (!request.Confirm)
        {
            throw new ArgumentException("confirm must be true before attendance records can be deleted.");
        }

        if (request.Before.HasValue && (request.From.HasValue || request.To.HasValue))
        {
            throw new ArgumentException("before cannot be combined with from or to.");
        }

        if (request.From.HasValue != request.To.HasValue)
        {
            throw new ArgumentException("from and to must be supplied together.");
        }

        if (request.From.HasValue && request.From.Value > request.To!.Value)
        {
            throw new ArgumentException("from must be earlier than or equal to to.");
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
