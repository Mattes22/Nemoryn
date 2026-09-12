namespace Memory.Application.Abstractions.AI;

using Memory.Domain.Conversations;
using Memory.Domain.Memories;

public interface IMemoryExtractor
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<ExtractedMemoryCandidate>> ExtractAsync(
        Conversation conversation,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);
}
