namespace Memory.Application.Abstractions.Persistence;

using Memory.Domain.Memories;

public sealed record MemorySimilarity(Memory Memory, double Distance);
