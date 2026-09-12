namespace Memory.Application.Abstractions.AI;

public interface IChatModelResolver
{
    Task<IReadOnlyList<string>> ListChatModelsAsync(CancellationToken cancellationToken = default);
    Task<string> ResolveChatModelAsync(string? requested, CancellationToken cancellationToken = default);
}
