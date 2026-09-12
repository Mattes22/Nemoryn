namespace Memory.Infrastructure.AI;

internal sealed record OllamaChatStreamPayload(
    OllamaChatMessage? Message,
    bool Done);
