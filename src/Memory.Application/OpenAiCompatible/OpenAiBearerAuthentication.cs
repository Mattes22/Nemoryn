namespace Memory.Application.OpenAiCompatible;

using System.Security.Cryptography;
using System.Text;

public static class OpenAiBearerAuthentication
{
    public static bool TryGetBearerToken(string? authorization, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return false;
        }

        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = authorization[prefix.Length..].Trim();
        return token.Length > 0;
    }

    public static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
