namespace Memory.Application.Tests.Fakes;

using Memory.Application.Abstractions.AI;

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public bool IsAvailable { get; set; } = true;

    public Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalized = input.Trim().ToLowerInvariant();
        var embedding = new float[8];

        for (var index = 0; index < normalized.Length; index++)
        {
            embedding[index % embedding.Length] += (normalized[index] % 13) / 13f;
        }

        return Task.FromResult<IReadOnlyList<float>>(embedding);
    }
}
