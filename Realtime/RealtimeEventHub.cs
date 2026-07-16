using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ZktecoRelay.Realtime;

public sealed class RealtimeEventHub
{
    private readonly ConcurrentDictionary<Guid, Channel<RealtimeEvent>> _subscribers = new();

    public RealtimeSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<RealtimeEvent>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;
        return new RealtimeSubscription(id, channel.Reader, () => _subscribers.TryRemove(id, out _));
    }

    public void Publish(RealtimeEvent realtimeEvent)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(realtimeEvent);
        }
    }
}

public sealed class RealtimeSubscription : IDisposable
{
    private readonly Action _unsubscribe;
    private int _disposed;

    internal RealtimeSubscription(Guid id, ChannelReader<RealtimeEvent> reader, Action unsubscribe)
    {
        Id = id;
        Reader = reader;
        _unsubscribe = unsubscribe;
    }

    public Guid Id { get; }
    public ChannelReader<RealtimeEvent> Reader { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unsubscribe();
        }
    }
}
