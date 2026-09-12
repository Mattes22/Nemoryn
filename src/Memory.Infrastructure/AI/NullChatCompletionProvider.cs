namespace Memory.Infrastructure.AI;

using Memory.Application.Abstractions.AI;

internal sealed class NullChatCompletionProvider : IChatCompletionProvider
{
    public bool IsAvailable => false;
    public string ProviderName => "None";
    public string ModelName => string.Empty;

    public Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Chat completion provider is not configured.");
    }
}
