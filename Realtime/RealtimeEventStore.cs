using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ZktecoRelay.Persistence;

namespace ZktecoRelay.Realtime;

public sealed class RealtimeEventStore
{
    public const int MaximumRetainedEvents = 100_000;
    public static readonly TimeSpan MaximumRetentionAge = TimeSpan.FromDays(30);

    private readonly RelayDatabase _database;
    private readonly int _maximumRetainedEvents;
    private readonly TimeSpan _maximumRetentionAge;
    private readonly object _sync = new();

    public RealtimeEventStore(
        RelayDatabase database,
        int maximumRetainedEvents = MaximumRetainedEvents,
        TimeSpan? maximumRetentionAge = null)
    {
        if (maximumRetainedEvents < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRetainedEvents));
        }

        _database = database;
        _maximumRetainedEvents = maximumRetainedEvents;
        _maximumRetentionAge =
            maximumRetentionAge ?? MaximumRetentionAge;
    }

    public RealtimeEvent Append(RealtimeEvent realtimeEvent)
    {
        lock (_sync)
        {
            using var connection = _database.Open();
            using var transaction = connection.BeginTransaction();
            var sequence = TryGetSequence(
                connection,
                transaction,
                realtimeEvent.EventId);
            if (!sequence.HasValue)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO realtime_events
                        (event_id, device_id, event_type, occurred_at, data_json, created_at)
                    VALUES
                        ($eventId, $deviceId, $eventType, $occurredAt, $dataJson, $createdAt);
                    SELECT last_insert_rowid();
                    """;
                insert.Parameters.AddWithValue("$eventId", realtimeEvent.EventId);
                insert.Parameters.AddWithValue("$deviceId", realtimeEvent.DeviceId);
                insert.Parameters.AddWithValue("$eventType", realtimeEvent.EventType);
                insert.Parameters.AddWithValue(
                    "$occurredAt",
                    realtimeEvent.OccurredAt.ToUniversalTime().ToString("O"));
                insert.Parameters.AddWithValue(
                    "$dataJson",
                    JsonSerializer.Serialize(
                        realtimeEvent.Data,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                insert.Parameters.AddWithValue(
                    "$createdAt",
                    DateTimeOffset.UtcNow.ToString("O"));
                sequence = Convert.ToInt64(
                    insert.ExecuteScalar(),
                    CultureInfo.InvariantCulture);
            }

            ApplyRetention(connection, transaction);
            transaction.Commit();
            return realtimeEvent with
            {
                EventSequence = sequence.Value.ToString(
                    CultureInfo.InvariantCulture)
            };
        }
    }

    public IReadOnlyList<RealtimeEvent> ReadAfter(long sequence, int limit)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        if (limit is < 1 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        lock (_sync)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT sequence, event_id, device_id, event_type, occurred_at, data_json
                FROM realtime_events
                WHERE sequence > $sequence
                ORDER BY sequence
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$sequence", sequence);
            command.Parameters.AddWithValue("$limit", limit);

            using var reader = command.ExecuteReader();
            var result = new List<RealtimeEvent>();
            while (reader.Read())
            {
                result.Add(ReadEvent(reader));
            }

            return result;
        }
    }

    public (long? Earliest, long? Latest) GetSequenceRange()
    {
        lock (_sync)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT MIN(sequence), MAX(sequence) FROM realtime_events;";
            using var reader = command.ExecuteReader();
            if (!reader.Read() || reader.IsDBNull(0))
            {
                return (null, null);
            }

            return (reader.GetInt64(0), reader.GetInt64(1));
        }
    }

    public void VerifyRead()
    {
        _ = GetSequenceRange();
    }

    private static long? TryGetSequence(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string eventId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT sequence FROM realtime_events WHERE event_id = $eventId;";
        command.Parameters.AddWithValue("$eventId", eventId);
        var result = command.ExecuteScalar();
        return result is null
            ? null
            : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private void ApplyRetention(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM realtime_events
            WHERE occurred_at < $oldestOccurredAt;

            DELETE FROM realtime_events
            WHERE sequence <= COALESCE((
                SELECT sequence
                FROM realtime_events
                ORDER BY sequence DESC
                LIMIT 1 OFFSET $maximumOffset
            ), 0);
            """;
        command.Parameters.AddWithValue(
            "$oldestOccurredAt",
            DateTimeOffset.UtcNow
                .Subtract(_maximumRetentionAge)
                .ToString("O"));
        command.Parameters.AddWithValue(
            "$maximumOffset",
            _maximumRetainedEvents);
        command.ExecuteNonQuery();
    }

    private static RealtimeEvent ReadEvent(SqliteDataReader reader)
    {
        var data =
            JsonSerializer.Deserialize<Dictionary<string, object?>>(
                reader.GetString(5),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new Dictionary<string, object?>();

        return new RealtimeEvent(
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTimeOffset.Parse(
                reader.GetString(4),
                CultureInfo.InvariantCulture),
            data,
            reader.GetInt64(0).ToString(CultureInfo.InvariantCulture));
    }
}
