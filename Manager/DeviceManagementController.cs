using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ZktecoRelay.Devices;
using ZktecoRelay.Models;
using ZktecoRelay.Persistence;

namespace ZktecoRelay.Manager;

internal sealed record ManagedDeviceRow(
    string DeviceId,
    string IpAddress,
    int Port,
    bool AutoConnect,
    bool RelayRunning,
    bool? Connected,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? LastCommunicationAt,
    DateTimeOffset? DisconnectedAt,
    int? ReconnectAttempt,
    DateTimeOffset? NextReconnectAt,
    string? LastError,
    DateTimeOffset UpdatedAt)
{
    public string Endpoint => $"{IpAddress}:{Port}";

    public string ConnectionState => !RelayRunning
        ? "Relay 已停止"
        : Connected == true
            ? "在线"
            : ReconnectAttempt is > 0
                ? $"重连中 #{ReconnectAttempt}"
                : AutoConnect
                    ? "等待连接"
                    : "未连接";

    public string AutoConnectText => AutoConnect ? "已启用" : "已停用";

    public string LastCommunicationText =>
        FormatLocalTime(LastCommunicationAt);

    public string NextReconnectText =>
        FormatLocalTime(NextReconnectAt);

    public string UpdatedAtText =>
        UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatLocalTime(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
}

internal sealed record DeviceManagementSnapshot(
    string DatabasePath,
    bool RelayRunning,
    IReadOnlyList<ManagedDeviceRow> Devices)
{
    public int OnlineCount =>
        Devices.Count(device =>
            device.RelayRunning &&
            device.Connected == true);

    public int ReconnectingCount =>
        Devices.Count(device =>
            device.RelayRunning &&
            device.Connected != true &&
            device.ReconnectAttempt is > 0);
}

internal sealed class DeviceManagementController
{
    private readonly Func<WebApplication?> _applicationAccessor;

    public DeviceManagementController(
        Func<WebApplication?> applicationAccessor)
    {
        _applicationAccessor = applicationAccessor;
    }

    public DeviceManagementSnapshot LoadSnapshot()
    {
        var context = ResolveContext();
        var statuses = context.Manager?
            .GetStatuses()
            .ToDictionary(status => status.DeviceId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, DeviceStatus>(StringComparer.OrdinalIgnoreCase);

        return Merge(
            context.Database.DatabasePath,
            context.Manager is not null,
            context.Store.GetViews(),
            statuses.Values);
    }

    public async Task<DeviceConnectionResult> ConnectAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var context = ResolveRunningContext();
        var configuration = context.Store.Get(deviceId)
            ?? throw new KeyNotFoundException(
                $"设备配置“{deviceId}”不存在。");

        context.Store.SetAutoConnect(deviceId, enabled: true);
        return await context.Manager!.RestoreAsync(
            configuration with { AutoConnect = true },
            cancellationToken);
    }

    public async Task<bool> DisconnectAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var context = ResolveRunningContext();
        return await context.Manager!.DisconnectAsync(
            deviceId,
            cancellationToken);
    }

    public bool SetAutoConnect(string deviceId, bool enabled)
    {
        var context = ResolveContext();
        return context.Store.SetAutoConnect(deviceId, enabled);
    }

    public async Task<bool> DeleteAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var context = ResolveContext();
        context.Store.SetAutoConnect(deviceId, enabled: false);

        if (context.Manager?.GetStatus(deviceId) is not null)
        {
            await context.Manager.DisconnectAsync(
                deviceId,
                cancellationToken);
        }

        return context.Store.Delete(deviceId);
    }

    public Task<UserPhotoResult> DownloadVisibleLightFacePhotoAsync(
        string deviceId,
        string enrollNumber,
        CancellationToken cancellationToken)
    {
        var context = ResolveRunningContext();
        return context.Manager!.DownloadVisibleLightFacePhotoAsync(
            deviceId,
            enrollNumber,
            cancellationToken);
    }

    internal static DeviceManagementSnapshot Merge(
        string databasePath,
        bool relayRunning,
        IReadOnlyList<DeviceConfigurationView> configurations,
        IEnumerable<DeviceStatus> statuses)
    {
        var statusByDevice = statuses.ToDictionary(
            status => status.DeviceId,
            StringComparer.OrdinalIgnoreCase);
        var rows = configurations
            .Select(configuration =>
            {
                statusByDevice.TryGetValue(
                    configuration.DeviceId,
                    out var status);
                return new ManagedDeviceRow(
                    configuration.DeviceId,
                    configuration.IpAddress,
                    configuration.Port,
                    configuration.AutoConnect,
                    relayRunning,
                    status?.Connected,
                    status?.ConnectedAt,
                    status?.LastCommunicationAt,
                    status?.DisconnectedAt,
                    status?.ReconnectAttempt,
                    status?.NextReconnectAt,
                    status?.LastError,
                    configuration.UpdatedAt);
            })
            .OrderBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DeviceManagementSnapshot(
            databasePath,
            relayRunning,
            rows);
    }

    private AdministrationContext ResolveRunningContext()
    {
        var context = ResolveContext();
        if (context.Manager is null)
        {
            throw new InvalidOperationException(
                "请先启动 Relay API，再执行连接或断开操作。");
        }

        return context;
    }

    private AdministrationContext ResolveContext()
    {
        var services = _applicationAccessor()?.Services;
        var database = services?.GetService<RelayDatabase>()
            ?? new RelayDatabase();
        var store = services?.GetService<DeviceConfigurationStore>()
            ?? new DeviceConfigurationStore(database);
        var manager = services?.GetService<DeviceManager>();
        return new AdministrationContext(database, store, manager);
    }

    private sealed record AdministrationContext(
        RelayDatabase Database,
        DeviceConfigurationStore Store,
        DeviceManager? Manager);
}
