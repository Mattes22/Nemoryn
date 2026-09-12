namespace Memory.Application.Abstractions.AI;

public static class AiModelName
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^7]
            : trimmed;
    }

    public static bool Equals(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return a.Length > 0
            && b.Length > 0
            && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static string Require(string? value, string message)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException(message);
        }

        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Model name is too long.");
        }

        return trimmed;
    }
}
