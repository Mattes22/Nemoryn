namespace Memory.Application.Runtime;

public static class MemoryDatabaseConnectionString
{
    public const int DefaultPort = 5432;

    public static MemoryDatabaseConnectionSettings Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Database connection string is required.", nameof(connectionString));
        }

        string? host = null;
        var port = DefaultPort;
        string? database = null;
        string? username = null;
        var password = string.Empty;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                host = value;
            }
            else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var parsedPort))
            {
                port = parsedPort;
            }
            else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Db", StringComparison.OrdinalIgnoreCase))
            {
                database = value;
            }
            else if (key.Equals("Username", StringComparison.OrdinalIgnoreCase)
                || key.Equals("User ID", StringComparison.OrdinalIgnoreCase)
                || key.Equals("UserId", StringComparison.OrdinalIgnoreCase)
                || key.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                username = value;
            }
            else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
            {
                password = value;
            }
        }

        return Normalize(host, port, database, username, password);
    }

    public static string Build(MemoryDatabaseConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(
            settings.Host,
            settings.Port,
            settings.Database,
            settings.Username,
            settings.Password);

        var parts = new List<string>
        {
            $"Host={normalized.Host}",
            $"Port={normalized.Port}",
            $"Database={normalized.Database}",
            $"Username={normalized.Username}"
        };

        if (!string.IsNullOrEmpty(normalized.Password))
        {
            parts.Add($"Password={normalized.Password}");
        }

        return string.Join(";", parts);
    }

    public static MemoryDatabaseConnectionSettings FromRequest(
        MemoryDatabaseConnectionRequest request,
        MemoryDatabaseConnectionSettings current)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(current);

        var password = string.IsNullOrWhiteSpace(request.Password)
            ? current.Password
            : request.Password.Trim();

        return Normalize(request.Host, request.Port, request.Database, request.Username, password);
    }

    public static MemoryDatabaseConnectionSettings Normalize(
        string? host,
        int port,
        string? database,
        string? username,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Database host is required.", nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentException("Database port must be between 1 and 65535.", nameof(port));
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentException("Database name is required.", nameof(database));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Database username is required.", nameof(username));
        }

        return new MemoryDatabaseConnectionSettings(
            host.Trim(),
            port,
            database.Trim(),
            username.Trim(),
            password?.Trim() ?? string.Empty);
    }
}
