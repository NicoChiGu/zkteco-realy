using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ZktecoRelay.Persistence;

public sealed class DeviceConfigurationStore
{
    private readonly string _connectionString;
    private readonly object _sync = new();

    public DeviceConfigurationStore()
    {
        var configuredPath = Environment.GetEnvironmentVariable("ZKTECO_DATABASE_PATH");
        string databasePath;

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            databasePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
        }
        else
        {
            var baseDataDir = Path.Combine(AppContext.BaseDirectory, "data");
            if (IsDirectoryWritable(baseDataDir))
            {
                databasePath = Path.Combine(baseDataDir, "zkteco-relay.db");
            }
            else
            {
                var appDataDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ZktecoRelay",
                    "data");
                databasePath = Path.Combine(appDataDataDir, "zkteco-relay.db");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Initialize();
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var testFile = Path.Combine(directoryPath, $".perm_test_{Guid.NewGuid():N}.tmp");
            using (var stream = File.Create(testFile, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<DeviceConfigurationView> GetViews()
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT device_id, ip_address, port, communication_password, auto_connect, updated_at
                FROM device_configurations
                ORDER BY device_id COLLATE NOCASE;
                """;

            using var reader = command.ExecuteReader();
            var result = new List<DeviceConfigurationView>();
            while (reader.Read())
            {
                result.Add(new DeviceConfigurationView(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetFieldValue<byte[]>(3).Length > 0,
                    reader.GetBoolean(4),
                    DateTimeOffset.Parse(reader.GetString(5))));
            }

            return result;
        }
    }

    public DeviceConfiguration? Get(string deviceId)
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT device_id, ip_address, port, communication_password, auto_connect, updated_at
                FROM device_configurations
                WHERE device_id = $deviceId COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);

            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadConfiguration(reader) : null;
        }
    }

    public IReadOnlyList<DeviceConfiguration> GetAutoConnectConfigurations()
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT device_id, ip_address, port, communication_password, auto_connect, updated_at
                FROM device_configurations
                WHERE auto_connect = 1
                ORDER BY device_id COLLATE NOCASE;
                """;

            using var reader = command.ExecuteReader();
            var result = new List<DeviceConfiguration>();
            while (reader.Read())
            {
                result.Add(ReadConfiguration(reader));
            }

            return result;
        }
    }

    public void Upsert(string deviceId, string ipAddress, int port, string communicationPassword, bool autoConnect)
    {
        var encryptedPassword = Protect(communicationPassword ?? string.Empty);
        var now = DateTimeOffset.UtcNow.ToString("O");

        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO device_configurations
                    (device_id, ip_address, port, communication_password, auto_connect, updated_at)
                VALUES
                    ($deviceId, $ipAddress, $port, $password, $autoConnect, $updatedAt)
                ON CONFLICT(device_id) DO UPDATE SET
                    ip_address = excluded.ip_address,
                    port = excluded.port,
                    communication_password = excluded.communication_password,
                    auto_connect = excluded.auto_connect,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            command.Parameters.AddWithValue("$ipAddress", ipAddress);
            command.Parameters.AddWithValue("$port", port);
            command.Parameters.Add("$password", SqliteType.Blob).Value = encryptedPassword;
            command.Parameters.AddWithValue("$autoConnect", autoConnect ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.ExecuteNonQuery();
        }
    }

    public bool SetAutoConnect(string deviceId, bool enabled)
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE device_configurations
                SET auto_connect = $enabled, updated_at = $updatedAt
                WHERE device_id = $deviceId COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$deviceId", deviceId);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public bool Delete(string deviceId)
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM device_configurations WHERE device_id = $deviceId COLLATE NOCASE;";
            command.Parameters.AddWithValue("$deviceId", deviceId);
            return command.ExecuteNonQuery() > 0;
        }
    }

    private void Initialize()
    {
        lock (_sync)
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
                """;
            command.ExecuteNonQuery();
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static DeviceConfiguration ReadConfiguration(SqliteDataReader reader)
    {
        var encryptedPassword = reader.GetFieldValue<byte[]>(3);
        return new DeviceConfiguration(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            Unprotect(encryptedPassword),
            reader.GetBoolean(4),
            DateTimeOffset.Parse(reader.GetString(5)));
    }

    private static byte[] Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Dpapi.Protect(bytes);
    }

    private static string Unprotect(byte[] value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(Dpapi.Unprotect(value));
    }

    private static class Dpapi
    {
        private const int CryptprotectLocalMachine = 0x4;

        public static byte[] Protect(byte[] input) => Transform(input, protect: true);
        public static byte[] Unprotect(byte[] input) => Transform(input, protect: false);

        private static byte[] Transform(byte[] input, bool protect)
        {
            var inputBlob = new DataBlob();
            var outputBlob = new DataBlob();
            try
            {
                inputBlob.Size = input.Length;
                inputBlob.Data = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inputBlob.Data, input.Length);

                var success = protect
                    ? CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptprotectLocalMachine, ref outputBlob)
                    : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptprotectLocalMachine, ref outputBlob);

                if (!success)
                {
                    throw new InvalidOperationException($"Windows DPAPI operation failed. Win32 error: {Marshal.GetLastWin32Error()}.");
                }

                var result = new byte[outputBlob.Size];
                Marshal.Copy(outputBlob.Data, result, 0, outputBlob.Size);
                return result;
            }
            finally
            {
                if (inputBlob.Data != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(inputBlob.Data);
                }

                if (outputBlob.Data != IntPtr.Zero)
                {
                    LocalFree(outputBlob.Data);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
