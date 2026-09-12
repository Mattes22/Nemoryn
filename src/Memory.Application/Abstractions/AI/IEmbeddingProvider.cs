namespace Memory.Application.Abstractions.AI;

public interface IEmbeddingProvider
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default);
}
