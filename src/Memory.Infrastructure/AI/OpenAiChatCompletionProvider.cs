namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;

internal sealed class OpenAiChatCompletionProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IChatCompletionProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OpenAiAvailability.IsConfigured(memoryAiOptions.CurrentValue);
    public string ProviderName => "OpenAI";
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
            "chat/completions",
            ChatCompletionPayload.ForOpenAi(model, stream: false, request),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Chat completion response was empty.");
        var message = payload.Choices?.FirstOrDefault()?.Message;
        var toolCalls = ChatCompletionPayload.ParseOpenAi(message?.ToolCalls);
        var content = message?.Content?.Trim() ?? string.Empty;
        if (toolCalls.Count == 0 && string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Chat completion response did not contain message content.");
        }

        return new ChatCompletionResponse(content, toolCalls);
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
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(
                ChatCompletionPayload.ForOpenAi(model, stream: true, request))
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
                if (json == "[DONE]")
                {
                    break;
                }

                continue;
            }

            var payload = JsonSerializer.Deserialize<OpenAiChatStreamResponse>(json, SerializerOptions);
            var delta = payload?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                yielded = true;
                yield return delta;
            }
        }

        if (!yielded)
        {
            throw new InvalidOperationException("Chat completion response did not contain message content.");
        }
    }

    private sealed record OpenAiChatCompletionResponse(IReadOnlyList<OpenAiChatChoice>? Choices);

    private sealed record OpenAiChatChoice(OpenAiChatMessage? Message);

    private sealed record OpenAiChatMessage(
        string? Content,
        IReadOnlyList<OpenAiProviderToolCall>? ToolCalls = null);

    private sealed record OpenAiChatStreamResponse(IReadOnlyList<OpenAiChatStreamChoice>? Choices);

    private sealed record OpenAiChatStreamChoice(OpenAiChatStreamDelta? Delta);

    private sealed record OpenAiChatStreamDelta(string? Content);
}
