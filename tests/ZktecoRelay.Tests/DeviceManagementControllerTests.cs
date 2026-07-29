using ZktecoRelay.Manager;
using ZktecoRelay.Models;
using ZktecoRelay.Persistence;

namespace ZktecoRelay.Tests;

public sealed class DeviceManagementControllerTests
{
    [Fact]
    public void MergeCombinesPersistedConfigurationWithRuntimeStatus()
    {
        var updatedAt = DateTimeOffset.Parse("2026-07-30T10:00:00Z");
        var lastCommunicationAt =
            DateTimeOffset.Parse("2026-07-30T10:01:00Z");
        var configurations = new[]
        {
            new DeviceConfigurationView(
                "front-door",
                "192.168.1.10",
                4370,
                HasCommunicationPassword: true,
                AutoConnect: true,
                updatedAt),
            new DeviceConfigurationView(
                "warehouse",
                "192.168.1.11",
                4370,
                HasCommunicationPassword: false,
                AutoConnect: false,
                updatedAt)
        };
        var statuses = new[]
        {
            new DeviceStatus(
                "front-door",
                "192.168.1.10",
                4370,
                Connected: true,
                ConnectedAt: updatedAt,
                LastError: null,
                LastCommunicationAt: lastCommunicationAt)
        };

        var snapshot = DeviceManagementController.Merge(
            @"C:\relay\data\zkteco-relay.db",
            relayRunning: true,
            configurations,
            statuses);

        Assert.Equal(2, snapshot.Devices.Count);
        Assert.Equal(1, snapshot.OnlineCount);
        Assert.Equal("在线", snapshot.Devices[0].ConnectionState);
        Assert.Equal("192.168.1.10:4370", snapshot.Devices[0].Endpoint);
        Assert.Equal(
            lastCommunicationAt.ToLocalTime().ToString(
                "yyyy-MM-dd HH:mm:ss"),
            snapshot.Devices[0].LastCommunicationText);
        Assert.Equal("未连接", snapshot.Devices[1].ConnectionState);
    }

    [Fact]
    public void MergeShowsReconnectAndStoppedStatesWithoutInventingOnlineStatus()
    {
        var updatedAt = DateTimeOffset.Parse("2026-07-30T10:00:00Z");
        var configuration = new DeviceConfigurationView(
            "front-door",
            "192.168.1.10",
            4370,
            HasCommunicationPassword: true,
            AutoConnect: true,
            updatedAt);
        var reconnecting = new DeviceStatus(
            "front-door",
            "192.168.1.10",
            4370,
            Connected: false,
            ConnectedAt: null,
            LastError: "socket closed",
            ReconnectAttempt: 3,
            NextReconnectAt: updatedAt.AddSeconds(10));

        var running = DeviceManagementController.Merge(
            "relay.db",
            relayRunning: true,
            [configuration],
            [reconnecting]);
        var stopped = DeviceManagementController.Merge(
            "relay.db",
            relayRunning: false,
            [configuration],
            []);

        Assert.Equal("重连中 #3", running.Devices.Single().ConnectionState);
        Assert.Equal(1, running.ReconnectingCount);
        Assert.Equal(
            "Relay 已停止",
            stopped.Devices.Single().ConnectionState);
        Assert.Equal(0, stopped.OnlineCount);
    }
}
