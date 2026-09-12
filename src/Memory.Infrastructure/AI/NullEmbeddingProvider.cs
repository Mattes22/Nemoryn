namespace Memory.Infrastructure.AI;

using Memory.Application.Abstractions.AI;

internal sealed class NullEmbeddingProvider : IEmbeddingProvider
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Embedding provider is not configured.");
    }
}
