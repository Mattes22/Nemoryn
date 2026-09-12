namespace Memory.Application.Tests.Fakes;

using Memory.Application.Abstractions.AI;
using Memory.Domain.Conversations;

internal sealed class FakeMemoryExtractor : IMemoryExtractor
{
    public bool IsAvailable { get; set; }

    public IReadOnlyList<ExtractedMemoryCandidate> Candidates { get; set; } = [];

    public Task<IReadOnlyList<ExtractedMemoryCandidate>> ExtractAsync(
        Conversation conversation,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Candidates);
    }
}
