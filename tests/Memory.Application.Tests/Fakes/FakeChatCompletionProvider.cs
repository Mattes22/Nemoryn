namespace Memory.Application.Tests.Fakes;

using Memory.Application.Abstractions.AI;

internal sealed class FakeChatCompletionProvider : IChatCompletionProvider
{
    public bool IsAvailable { get; set; } = true;
    public string ProviderName { get; set; } = "Fake";
    public string ModelName { get; set; } = "fake-chat";
    public string Response { get; set; } = "Assistant response.";
    public Exception? Exception { get; set; }
    public ChatCompletionRequest? LastRequest { get; private set; }
    public List<ChatCompletionRequest> Requests { get; } = [];
    public Queue<ChatCompletionResponse> Completions { get; } = new();

    public Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        Requests.Add(request);
        BeforeComplete?.Invoke();
        if (Exception is not null)
        {
            throw Exception;
        }

        if (Completions.Count > 0)
        {
            return Task.FromResult(Completions.Dequeue());
        }

        return Task.FromResult(new ChatCompletionResponse(Response));
    }

    public Action? BeforeComplete { get; set; }
}
