namespace Memory.Application.Abstractions.AI;

using System.Runtime.CompilerServices;

public interface IChatCompletionProvider
{
    bool IsAvailable { get; }
    string ProviderName { get; }
    string ModelName { get; }

    Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
        => StreamFromCompleteAsync(this, request, cancellationToken);

    private static async IAsyncEnumerable<string> StreamFromCompleteAsync(
        IChatCompletionProvider provider,
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var completion = await provider.CompleteAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(completion.Content))
        {
            yield return completion.Content;
        }
    }
}
