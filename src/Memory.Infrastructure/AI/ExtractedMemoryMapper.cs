namespace Memory.Infrastructure.AI;

using System.Text;
using Memory.Application.Abstractions.AI;
using Memory.Application.Memories;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;

internal static class ExtractedMemoryMapper
{
    public const string SystemPrompt = """
        Extract durable memories the way ChatGPT, Claude or Gemini would.
        Return JSON: {"memories":[{"content":"","type":"Fact","scope":"User","importance":0.5,"confidence":0.5,"validFrom":null,"validUntil":null,"sourceSummary":""}]}
        Allowed types: Fact, Preference, Goal, Constraint, Relationship, Summary.
        Allowed scopes: User, Conversation.
        Use User scope for lasting identity: name, language, location, preferences, constraints, relationships, long-term goals.
        Use Conversation scope only for a compact summary of this thread.
        importance and confidence must be numbers between 0 and 1.
        Extract memories only from the chat message contents.
        Never extract internal metadata such as owner id, conversation id, external id, database ids, timestamps, or API fields.
        Technical identifiers are routing data, not facts about the person.
        Write each memory as a stable canonical fact in nominative form, not inflected case (say "User lives in Tučapy", not "User lives in Tučapech").
        Do not infer a language preference just because the user writes in that language. Store language only when the user explicitly asks for it.
        Only extract information that should still be true in a new chat next month.
        Do not extract greetings, jokes, one-off tasks, or things the user is only trying right now.
        Prefer few high-quality memories over many weak ones.
        If there is nothing worth remembering, return {"memories":[]}.
        """;

    public static IReadOnlyList<ExtractedMemoryCandidate> Map(IEnumerable<ExtractedMemoryJson>? memories)
    {
        if (memories is null)
        {
            return [];
        }

        return memories
            .Select(TryMap)
            .OfType<ExtractedMemoryCandidate>()
            .Take(8)
            .ToArray();
    }

    private static ExtractedMemoryCandidate? TryMap(ExtractedMemoryJson candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Content)
            || !Enum.TryParse<MemoryType>(candidate.Type, ignoreCase: true, out var type)
            || !Enum.IsDefined(type))
        {
            return null;
        }

        var scope = MemoryScopeRules.DefaultFor(type);
        if (Enum.TryParse<MemoryScope>(candidate.Scope, ignoreCase: true, out var parsedScope)
            && Enum.IsDefined(parsedScope))
        {
            scope = parsedScope;
        }

        DateTimeOffset? validFrom = null;
        DateTimeOffset? validUntil = null;

        if (DateTimeOffset.TryParse(candidate.ValidFrom, out var parsedFrom))
        {
            validFrom = parsedFrom;
        }

        if (DateTimeOffset.TryParse(candidate.ValidUntil, out var parsedUntil))
        {
            validUntil = parsedUntil;
        }

        if (validFrom is not null && validUntil is not null && validUntil <= validFrom)
        {
            validUntil = null;
        }

        var content = candidate.Content.Trim();
        if (ExtractedMemoryMetadataFilter.LooksLikeMetadataField(content))
        {
            return null;
        }

        return new ExtractedMemoryCandidate(
            content,
            type,
            scope,
            ClampScore(candidate.Importance),
            ClampScore(candidate.Confidence),
            validFrom,
            validUntil,
            string.IsNullOrWhiteSpace(candidate.SourceSummary) ? null : candidate.SourceSummary.Trim());
    }

    public static string BuildUserPrompt(Conversation conversation, IReadOnlyList<Message> messages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Extract durable memories from these chat messages only.");
        builder.AppendLine("Do not treat routing data, identifiers, or titles as biographical facts.");

        if (!string.IsNullOrWhiteSpace(conversation.Title))
        {
            builder.AppendLine($"Thread title: {conversation.Title}");
        }

        builder.AppendLine("Messages:");

        foreach (var message in messages)
        {
            builder.AppendLine($"[{message.SequenceNumber}] {message.Role.ToString().ToLowerInvariant()}: {message.Content}");
        }

        return builder.ToString();
    }

    private static decimal ClampScore(decimal score)
    {
        if (score < 0m)
        {
            return 0m;
        }

        if (score > 1m)
        {
            return 1m;
        }

        return score;
    }
}

internal sealed record ExtractionPayload(IReadOnlyList<ExtractedMemoryJson>? Memories);

internal sealed record ExtractedMemoryJson(
    string? Content,
    string? Type,
    string? Scope,
    decimal Importance,
    decimal Confidence,
    string? ValidFrom,
    string? ValidUntil,
    string? SourceSummary);
