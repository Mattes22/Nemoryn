namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;

internal sealed class OllamaChatCompletionProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IChatCompletionProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OllamaAvailability.IsConfigured(memoryAiOptions.CurrentValue);
    public string ProviderName => "Ollama";
    public string ModelName => memoryAiOptions.CurrentValue.ChatModel;

    public async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(request));
        }

        var options = memoryAiOptions.CurrentValue;
        var model = string.IsNullOrWhiteSpace(request.Model) ? options.ChatModel : request.Model.Trim();
        var client = httpClientFactory.CreateClient("MemoryAi");
        using var response = await client.PostAsJsonAsync(
            "api/chat",
            ChatCompletionPayload.ForOllama(model, stream: false, request),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Ollama chat response was empty.");
        var toolCalls = ChatCompletionPayload.ParseOllama(payload.Message?.ToolCalls);
        var content = toolCalls.Count > 0
            ? payload.Message?.Content?.Trim() ?? string.Empty
            : OllamaMessageContent.ReadChat(payload.Message);
        if (toolCalls.Count == 0 && string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ollama chat response did not contain message content.");
        }

        return new ChatCompletionResponse(content ?? string.Empty, toolCalls);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(request));
        }

        var options = memoryAiOptions.CurrentValue;
        var model = string.IsNullOrWhiteSpace(request.Model) ? options.ChatModel : request.Model.Trim();
        var client = httpClientFactory.CreateClient("MemoryAiStream");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(
                ChatCompletionPayload.ForOllama(model, stream: true, request))
        };

        using var response = await client.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var yielded = false;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var json = line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? line[5..].Trim()
                : line.Trim();
            if (json.Length == 0 || json == "[DONE]")
            {
                continue;
            }

            var payload = JsonSerializer.Deserialize<OllamaChatStreamPayload>(json, SerializerOptions);
            var delta = payload?.Message?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                yielded = true;
                yield return delta;
            }

            if (payload?.Done == true)
            {
                break;
            }
        }

        if (!yielded)
        {
            throw new InvalidOperationException("Ollama chat response did not contain message content.");
        }
    }
}
