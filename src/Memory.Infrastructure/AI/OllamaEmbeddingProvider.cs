namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;

internal sealed class OllamaEmbeddingProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OllamaAvailability.IsConfigured(memoryAiOptions.CurrentValue);

    public async Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Embedding input is required.", nameof(input));
        }

        var options = memoryAiOptions.CurrentValue;
        var client = httpClientFactory.CreateClient("MemoryAi");
        using var response = await client.PostAsJsonAsync(
            "api/embeddings",
            new
            {
                model = options.EmbeddingModel,
                prompt = input.Trim()
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Ollama embedding response was empty.");

        return payload.Embedding ?? throw new InvalidOperationException("Ollama embedding response did not contain a vector.");
    }

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("embedding")] IReadOnlyList<float>? Embedding);
}
