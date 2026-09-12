namespace Memory.Application.Memories;

public interface IMemoryRetentionService
{
    Task<MemoryRetentionResult> ApplyAsync(CancellationToken cancellationToken = default);
}
