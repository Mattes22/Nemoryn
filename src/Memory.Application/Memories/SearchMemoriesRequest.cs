namespace Memory.Application.Memories;

public sealed record SearchMemoriesRequest(string Query, int? Limit);
