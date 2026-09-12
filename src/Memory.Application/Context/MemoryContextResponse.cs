namespace Memory.Application.Context;

public sealed record MemoryContextResponse(
    Guid ConversationId,
    string ContextText,
    IReadOnlyList<MemoryContextItem> Memories);
