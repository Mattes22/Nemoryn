namespace Memory.Application.Runtime;

using System.Text.Json;

internal sealed class FileToolsConnectionStore(string path) : IToolsConnectionStore
{
    public const string FileName = "memory-tools.connection.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object gate = new();

    public ToolsConnectionSettings? Load()
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
                var loaded = JsonSerializer.Deserialize<ToolsConnectionSettings>(json, JsonOptions);
                return loaded is null
                    ? null
                    : new ToolsConnectionSettings(loaded.SearXngBaseUrl?.Trim() ?? string.Empty);
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public bool TrySave(ToolsConnectionSettings settings, out string? error)
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
