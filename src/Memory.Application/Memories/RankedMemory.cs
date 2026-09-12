namespace Memory.Application.Memories;

using Memory.Domain.Memories;

public sealed record RankedMemory(MemoryResponse Memory, double Score, double? Similarity);
