namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;
using Memory.Domain.Conversations;

internal sealed class OllamaMemoryExtractor(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IMemoryExtractor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OllamaAvailability.IsConfigured(memoryAiOptions.CurrentValue);

    public async Task<IReadOnlyList<ExtractedMemoryCandidate>> ExtractAsync(
        Conversation conversation,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var options = memoryAiOptions.CurrentValue;
        var client = httpClientFactory.CreateClient("MemoryAi");
        using var response = await client.PostAsJsonAsync(
            "api/chat",
            new
            {
                model = options.ChatModel,
                stream = false,
                format = "json",
                messages = new object[]
                {
                    new { role = "system", content = ExtractedMemoryMapper.SystemPrompt },
                    new { role = "user", content = ExtractedMemoryMapper.BuildUserPrompt(conversation, messages) }
                }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Ollama memory extraction response was empty.");

        var content = OllamaMessageContent.ReadJson(payload.Message);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var parsed = JsonSerializer.Deserialize<ExtractionPayload>(content, SerializerOptions);
        return ExtractedMemoryMapper.Map(parsed?.Memories);
    }
}
