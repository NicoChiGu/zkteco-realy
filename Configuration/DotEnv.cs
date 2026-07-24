namespace ZktecoRelay.Configuration;

public static class DotEnv
{
    public static void AutoLoad()
    {
        var appDataEnv = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZktecoRelay",
            ".env");
        var baseEnv = Path.Combine(AppContext.BaseDirectory, ".env");
        var parentEnv = Path.Combine(AppContext.BaseDirectory, "..", ".env");
        var currentEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");

        Load(appDataEnv);
        Load(baseEnv);
        Load(parentEnv);
        Load(currentEnv);
    }

    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
