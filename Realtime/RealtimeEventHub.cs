using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ZktecoRelay.Realtime;

public sealed class RealtimeEventHub
{
    private readonly ConcurrentDictionary<Guid, Channel<bool>> _subscribers = new();
    private readonly RealtimeEventStore _store;

    public RealtimeEventHub(RealtimeEventStore store)
    {
        _store = store;
    }

    public RealtimeSubscription Subscribe(long afterSequence = 0)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;
        return new RealtimeSubscription(
            id,
            afterSequence,
            _store,
            channel.Reader,
            () => _subscribers.TryRemove(id, out _));
    }

    public void Publish(RealtimeEvent realtimeEvent)
    {
        _store.Append(realtimeEvent);
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(true);
        }
    }

    public (long? Earliest, long? Latest) GetSequenceRange() =>
        _store.GetSequenceRange();
}

public sealed class RealtimeSubscription : IDisposable
{
    private readonly RealtimeEventStore _store;
    private readonly ChannelReader<bool> _notifications;
    private readonly Action _unsubscribe;
    private long _cursor;
    private int _disposed;

    internal RealtimeSubscription(
        Guid id,
        long afterSequence,
        RealtimeEventStore store,
        ChannelReader<bool> notifications,
        Action unsubscribe)
    {
        Id = id;
        _cursor = afterSequence;
        _store = store;
        _notifications = notifications;
        _unsubscribe = unsubscribe;
    }

    public Guid Id { get; }
    public long Cursor => Interlocked.Read(ref _cursor);

    public IReadOnlyList<RealtimeEvent> ReadNextBatch(int limit = 200)
    {
        var result = _store.ReadAfter(Cursor, limit);
        if (result.Count > 0 &&
            long.TryParse(result[^1].EventSequence, out var sequence))
        {
            Interlocked.Exchange(ref _cursor, sequence);
        }

        return result;
    }

    public async Task WaitForEventsAsync(CancellationToken cancellationToken)
    {
        await _notifications.ReadAsync(cancellationToken);
        while (_notifications.TryRead(out _))
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unsubscribe();
        }
    }
}
