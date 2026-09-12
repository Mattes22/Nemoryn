namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;

internal sealed class OpenAiEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OpenAiAvailability.IsConfigured(memoryAiOptions.CurrentValue);

    public async Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Embedding input is required.", nameof(input));
        }

        var options = memoryAiOptions.CurrentValue;
        var client = httpClientFactory.CreateClient("MemoryAi");
        using var response = await client.PostAsJsonAsync(
            "embeddings",
            new
            {
                model = options.EmbeddingModel,
                input = input.Trim(),
                dimensions = options.EmbeddingDimensions
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Embedding response was empty.");

        var embedding = payload.Data?.FirstOrDefault()?.Embedding
            ?? throw new InvalidOperationException("Embedding response did not contain a vector.");

        return embedding;
    }

    private sealed record EmbeddingResponse(IReadOnlyList<EmbeddingData> Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("embedding")] IReadOnlyList<float> Embedding);
}
