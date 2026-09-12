namespace Memory.Application.Memories;

using System.Text.RegularExpressions;

public static partial class ExtractedMemoryMetadataFilter
{
    public static bool ShouldDiscard(
        string content,
        string ownerId,
        Guid conversationId,
        string? externalId)
    {
        var text = Normalize(content);
        if (text.Length == 0)
        {
            return true;
        }

        if (LooksLikeMetadataField(text))
        {
            return true;
        }

        foreach (var identifier in Identifiers(ownerId, conversationId, externalId))
        {
            if (IsIdentifierMemory(text, identifier))
            {
                return true;
            }
        }

        return false;
    }

    public static bool LooksLikeMetadataField(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return MetadataFieldPattern().IsMatch(Normalize(content));
    }

    private static bool IsIdentifierMemory(string text, string identifier)
    {
        var value = Normalize(identifier);
        if (value.Length < 2)
        {
            return false;
        }

        if (text.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!ContainsToken(text, value))
        {
            return false;
        }

        var escaped = Regex.Escape(value);
        return Regex.IsMatch(
            text,
            $@"\b(id|identifier|uuid|guid|key)\b[\s:=#-]*{escaped}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                text,
                $@"{escaped}[\s:=#-]*\b(id|identifier|uuid|guid|key)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                text,
                $@"\b(name|named|jmeno|jmenuje se)\b.{{0,24}}{escaped}\s*\.?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> Identifiers(string ownerId, Guid conversationId, string? externalId)
    {
        yield return ownerId;
        yield return conversationId.ToString();
        yield return conversationId.ToString("N");

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            yield return externalId;
        }
    }

    private static bool ContainsToken(string text, string value)
    {
        var start = 0;
        while (start <= text.Length - value.Length)
        {
            var index = text.IndexOf(value, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var before = index == 0 || !IsIdentifierCharacter(text[index - 1]);
            var afterIndex = index + value.Length;
            var after = afterIndex >= text.Length || !IsIdentifierCharacter(text[afterIndex]);
            if (before && after)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character is '_' or '-';
    }

    private static string Normalize(string value)
    {
        return value.Trim();
    }

    [GeneratedRegex(
        """
        \b(
            owner\s*id|ownerid|owner_id|
            conversation\s*id|conversationid|conversation_id|
            external\s*id|externalid|external_id|
            database\s*id|memory\s*id|
            user\s*id|userid|user_id
        )\b
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex MetadataFieldPattern();
}
