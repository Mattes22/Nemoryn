namespace Memory.Infrastructure.AI;

using Memory.Application.Abstractions.AI;

internal sealed class NullAiModelCatalog : IAiModelCatalog
{
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
