namespace Memory.Application.Tests.Fakes;

using Memory.Application.Abstractions.AI;

internal sealed class FakeAiModelCatalog : IAiModelCatalog
{
    public IReadOnlyList<string> Models { get; set; } = [];

    public Exception? Exception { get; set; }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Models);
    }
}
