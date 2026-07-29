using Microsoft.Data.Sqlite;

namespace ZktecoRelay.Persistence;

public sealed class RelayDatabase
{
    private readonly string _connectionString;
    private readonly object _migrationSync = new();

    public RelayDatabase()
    {
        var configuredPath = Environment.GetEnvironmentVariable("ZKTECO_DATABASE_PATH");
        string databasePath;

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            databasePath = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(configuredPath));
        }
        else
        {
            var baseDataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
            if (IsDirectoryWritable(baseDataDirectory))
            {
                databasePath = Path.Combine(baseDataDirectory, "zkteco-relay.db");
            }
            else
            {
                var appDataDirectory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "ZktecoRelay",
                    "data");
                databasePath = Path.Combine(
                    appDataDirectory,
                    "zkteco-relay.db");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Migrate();
    }

    public string DatabasePath { get; }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void VerifyReadWrite()
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO relay_health_probe (probe_key, checked_at)
            VALUES ('readiness', $checkedAt)
            ON CONFLICT(probe_key) DO UPDATE SET checked_at = excluded.checked_at;
            DELETE FROM relay_health_probe WHERE probe_key = 'readiness';
            """;
        command.Parameters.AddWithValue(
            "$checkedAt",
            DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private void Migrate()
    {
        lock (_migrationSync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS device_configurations (
                    device_id TEXT PRIMARY KEY COLLATE NOCASE,
                    ip_address TEXT NOT NULL,
                    port INTEGER NOT NULL,
                    communication_password BLOB NOT NULL,
                    auto_connect INTEGER NOT NULL DEFAULT 1,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS realtime_events (
                    sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                    event_id TEXT NOT NULL UNIQUE,
                    device_id TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    occurred_at TEXT NOT NULL,
                    data_json TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_realtime_events_occurred_at
                    ON realtime_events(occurred_at);
                CREATE TABLE IF NOT EXISTS relay_health_probe (
                    probe_key TEXT PRIMARY KEY,
                    checked_at TEXT NOT NULL
                );
                PRAGMA user_version = 2;
                """;
            command.ExecuteNonQuery();
        }
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            var testFile = Path.Combine(
                directoryPath,
                $".perm_test_{Guid.NewGuid():N}.tmp");
            using (File.Create(testFile, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
