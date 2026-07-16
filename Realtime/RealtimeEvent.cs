namespace ZktecoRelay.Realtime;

public sealed record RealtimeEvent(
    string EventId,
    string DeviceId,
    string EventType,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Data);
