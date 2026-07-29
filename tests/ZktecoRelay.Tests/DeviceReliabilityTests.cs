using System.Text.Json;
using ZktecoRelay.Devices;
using ZktecoRelay.Models;
using ZktecoRelay.Persistence;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Tests;

public sealed class DeviceReliabilityTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(
            Path.GetTempPath(),
            "zkteco-relay-device-tests",
            Guid.NewGuid().ToString("N"));

    public DeviceReliabilityTests()
    {
        Directory.CreateDirectory(_directory);
        Environment.SetEnvironmentVariable(
            "ZKTECO_DATABASE_PATH",
            Path.Combine(_directory, "relay.db"));
    }

    [Fact]
    public async Task PeriodicProbeMarksSocketDisconnectAndPublishesStatus()
    {
        var client = new ProbeControlledComClient();
        var database = new RelayDatabase();
        var eventStore = new RealtimeEventStore(database);
        var hub = new RealtimeEventHub(eventStore);
        using var session = new DeviceSession(
            "front-door",
            "192.168.1.10",
            4370,
            hub,
            new FakeComClientFactory(() => client),
            TimeSpan.FromMilliseconds(20));

        var connected =
            await session.ConnectAsync(string.Empty, CancellationToken.None);
        Assert.True(connected.Connected);

        client.ProbeConnected = false;
        await WaitUntilAsync(
            () => !session.GetStatus().Connected,
            TimeSpan.FromSeconds(2));

        var status = session.GetStatus();
        Assert.False(status.Connected);
        Assert.NotNull(status.DisconnectedAt);
        Assert.Contains(
            eventStore.ReadAfter(0, 100),
            item =>
                item.EventType == "device_status_changed" &&
                item.Data["connected"] is JsonElement element &&
                !element.GetBoolean());
    }

    [Fact]
    public async Task CommandFailureImmediatelyConfirmsSocketDisconnect()
    {
        var client = new ProbeControlledComClient();
        var database = new RelayDatabase();
        var hub = new RealtimeEventHub(new RealtimeEventStore(database));
        using var session = new DeviceSession(
            "front-door",
            "192.168.1.10",
            4370,
            hub,
            new FakeComClientFactory(() => client));

        await session.ConnectAsync(string.Empty, CancellationToken.None);
        client.FailUserRead = true;
        client.ProbeConnected = false;

        await Assert.ThrowsAsync<DeviceOperationException>(
            () => session.GetUsersAsync(CancellationToken.None));
        Assert.False(session.GetStatus().Connected);
    }

    [Fact]
    public async Task ExplicitDisconnectDisablesPersistedAutoConnect()
    {
        var database = new RelayDatabase();
        var store = new DeviceConfigurationStore(database);
        var hub = new RealtimeEventHub(new RealtimeEventStore(database));
        using var manager = new DeviceManager(
            store,
            hub,
            new FakeComClientFactory());

        await manager.ConnectAsync(
            "front-door",
            new ConnectDeviceRequest("192.168.1.10"),
            CancellationToken.None);
        Assert.True(store.Get("front-door")!.AutoConnect);

        await manager.DisconnectAsync(
            "front-door",
            CancellationToken.None);
        Assert.False(store.Get("front-door")!.AutoConnect);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 10)]
    [InlineData(4, 30)]
    [InlineData(5, 60)]
    [InlineData(8, 60)]
    public void RetryBackoffUsesDocumentedSchedule(
        int attempt,
        int expectedSeconds)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            DeviceAutoConnectService.GetDelay(attempt, 1));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ZKTECO_DATABASE_PATH", null);
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    "The expected device status was not observed.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class ProbeControlledComClient : FakeComClient
    {
        public bool ProbeConnected { get; set; } = true;
        public bool FailUserRead { get; set; }

        public override ConnectionProbeResult ProbeConnection() =>
            new(
                ProbeConnected,
                ProbeConnected ? 0 : -10004,
                ProbeConnected ? null : "socket disconnected");

        public override IReadOnlyList<UserInfo> GetUsers()
        {
            if (FailUserRead)
            {
                throw new DeviceOperationException(
                    "ReadAllUserID failed.",
                    -10004);
            }

            return [];
        }
    }
}
