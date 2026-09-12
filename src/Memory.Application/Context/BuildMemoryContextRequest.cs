namespace Memory.Application.Context;

public sealed record BuildMemoryContextRequest(
    string? Query,
    int? Limit,
    bool IncludeSourceTrace);
