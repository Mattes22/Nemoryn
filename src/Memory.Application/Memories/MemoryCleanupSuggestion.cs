namespace Memory.Application.Memories;

public sealed record MemoryCleanupSuggestion(
    Guid MemoryId,
    string ReasonCode,
    string Reason,
    MemoryResponse Memory);
