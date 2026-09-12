namespace Memory.Domain.Memories;

using System.Security.Cryptography;
using System.Text;

public static class MemoryFingerprint
{
    public static string Compute(string ownerId, MemoryScope scope, MemoryType type, string content)
    {
        var normalized = Normalize(content);
        var payload = $"{ownerId.Trim()}\n{scope}\n{type}\n{normalized}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Normalize(string content)
    {
        return string.Join(
            ' ',
            content.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
