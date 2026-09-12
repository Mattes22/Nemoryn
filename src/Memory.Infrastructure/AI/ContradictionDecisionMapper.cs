namespace Memory.Infrastructure.AI;

using Memory.Application.Abstractions.AI;

internal static class ContradictionDecisionMapper
{
    public const string SystemPrompt = """
        Decide whether a proposed long-term memory contradicts any existing active memory.
        Return JSON only: {"relation":"Unrelated|Contradicts","conflictingMemoryId":null,"confidence":0.0,"reason":""}
        Use Contradicts only for direct semantic conflict where both statements cannot be true at the same time.
        Mutually exclusive preferences, hometowns, or current facts are Contradicts (tea vs coffee, Holešov vs Tučapy).
        Inflected forms, locative/dative case, and paraphrases of the same fact are Unrelated, not Contradicts.
        Use Unrelated for different topics, weak evidence, duplicate, paraphrase, or reinforcing information.
        If relation is Contradicts, conflictingMemoryId must be one of the provided existing memory ids.
        confidence must be between 0 and 1.
        Keep reason short and factual.
        """;

    public static MemoryContradictionDecision Map(ContradictionDecisionJson? json)
    {
        if (json is null
            || !Enum.TryParse<MemoryContradictionRelation>(json.Relation, ignoreCase: true, out var relation)
            || !Enum.IsDefined(relation))
        {
            return None();
        }

        var confidence = Clamp(json.Confidence);
        var conflictingMemoryId = relation == MemoryContradictionRelation.Contradicts
            ? json.ConflictingMemoryId
            : null;

        return new MemoryContradictionDecision(
            relation,
            conflictingMemoryId,
            confidence,
            string.IsNullOrWhiteSpace(json.Reason) ? null : json.Reason.Trim());
    }

    public static MemoryContradictionDecision None()
    {
        return new MemoryContradictionDecision(
            MemoryContradictionRelation.Unrelated,
            null,
            0m,
            null);
    }

    private static decimal Clamp(decimal score)
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

internal sealed record ContradictionDecisionJson(
    string? Relation,
    Guid? ConflictingMemoryId,
    decimal Confidence,
    string? Reason);
