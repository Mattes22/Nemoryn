namespace Memory.Infrastructure.AI;

using Memory.Application.Abstractions.AI;
using Memory.Domain.Conversations;

internal sealed class NullMemoryExtractor : IMemoryExtractor
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<ExtractedMemoryCandidate>> ExtractAsync(
        Conversation conversation,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ExtractedMemoryCandidate>>([]);
    }
}
