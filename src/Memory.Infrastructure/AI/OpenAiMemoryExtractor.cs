namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;
using Memory.Domain.Conversations;

internal sealed class OpenAiMemoryExtractor(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IMemoryExtractor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OpenAiAvailability.IsConfigured(memoryAiOptions.CurrentValue);

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
            "chat/completions",
            new
            {
                model = options.ChatModel,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = ExtractedMemoryMapper.SystemPrompt },
                    new { role = "user", content = ExtractedMemoryMapper.BuildUserPrompt(conversation, messages) }
                }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Memory extraction response was empty.");

        var content = payload.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var parsed = JsonSerializer.Deserialize<ExtractionPayload>(content, SerializerOptions);
        return ExtractedMemoryMapper.Map(parsed?.Memories);
    }

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice(ChatMessage? Message);

    private sealed record ChatMessage(string? Content);
}
