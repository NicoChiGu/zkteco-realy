using ZktecoRelay.Persistence;
using ZktecoRelay.Realtime;

namespace ZktecoRelay.Tests;

public sealed class RealtimeEventStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(
            Path.GetTempPath(),
            "zkteco-relay-event-tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public void EventsReceiveMonotonicStringSequencesAndDuplicateIdsAreStable()
    {
        Directory.CreateDirectory(_directory);
        Environment.SetEnvironmentVariable(
            "ZKTECO_DATABASE_PATH",
            Path.Combine(_directory, "events.db"));
        var store = new RealtimeEventStore(new RelayDatabase());

        var first = store.Append(Event("event-1"));
        var second = store.Append(Event("event-2"));
        var duplicate = store.Append(Event("event-1"));

        Assert.Equal("1", first.EventSequence);
        Assert.Equal("2", second.EventSequence);
        Assert.Equal("1", duplicate.EventSequence);
        Assert.Equal(
            ["event-1", "event-2"],
            store.ReadAfter(0, 100)
                .Select(item => item.EventId)
                .ToArray());
    }

    [Fact]
    public void RetentionKeepsNewestRecordsAndSequenceSurvivesRestart()
    {
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "events.db");
        Environment.SetEnvironmentVariable(
            "ZKTECO_DATABASE_PATH",
            databasePath);
        var firstStore = new RealtimeEventStore(
            new RelayDatabase(),
            maximumRetainedEvents: 3);

        for (var index = 1; index <= 5; index++)
        {
            firstStore.Append(Event($"event-{index}"));
        }

        Assert.Equal(
            ["event-3", "event-4", "event-5"],
            firstStore.ReadAfter(0, 100)
                .Select(item => item.EventId)
                .ToArray());

        var restartedStore = new RealtimeEventStore(
            new RelayDatabase(),
            maximumRetainedEvents: 3);
        var afterRestart = restartedStore.Append(Event("event-6"));
        Assert.Equal("6", afterRestart.EventSequence);
        Assert.Equal(
            ["event-4", "event-5", "event-6"],
            restartedStore.ReadAfter(0, 100)
                .Select(item => item.EventId)
                .ToArray());
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

    private static RealtimeEvent Event(string eventId) =>
        new(
            eventId,
            "front-door",
            "door_state",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["open"] = true });
}
