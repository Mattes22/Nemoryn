namespace Memory.Application.Runtime;

using System.Text.Json;

internal sealed class FileMemoryDatabaseConnectionStore(string path) : IMemoryDatabaseConnectionStore
{
    public const string FileName = "memory-db.connection.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object gate = new();

    public MemoryDatabaseConnectionSettings? Load()
    {
        lock (gate)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<MemoryDatabaseConnectionSettings>(json, JsonOptions);
                if (loaded is null
                    || string.IsNullOrWhiteSpace(loaded.Host)
                    || string.IsNullOrWhiteSpace(loaded.Database)
                    || string.IsNullOrWhiteSpace(loaded.Username)
                    || loaded.Port is < 1 or > 65535)
                {
                    return null;
                }

                return MemoryDatabaseConnectionString.Normalize(
                    loaded.Host,
                    loaded.Port == 0 ? MemoryDatabaseConnectionString.DefaultPort : loaded.Port,
                    loaded.Database,
                    loaded.Username,
                    loaded.Password);
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }
    }

    public bool TrySave(MemoryDatabaseConnectionSettings settings, out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, path, overwrite: true);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }
    }
}
