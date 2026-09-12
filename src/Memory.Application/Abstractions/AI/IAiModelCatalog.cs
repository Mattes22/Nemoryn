namespace Memory.Application.Abstractions.AI;

public interface IAiModelCatalog
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
}
