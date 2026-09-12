namespace Memory.Application.Memories;

public sealed record MemorySearchMatch(MemoryResponse Memory, double Score, double? Similarity);
