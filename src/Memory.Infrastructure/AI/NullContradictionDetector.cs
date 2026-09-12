namespace Memory.Infrastructure.AI;

using Memory.Application.Abstractions.AI;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class NullContradictionDetector : IContradictionDetector
{
    public bool IsAvailable => false;

    public Task<MemoryContradictionDecision> DetectAsync(
        ExtractedMemoryCandidate candidate,
        IReadOnlyList<MemoryEntity> existingMemories,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MemoryContradictionDecision(
            MemoryContradictionRelation.Unrelated,
            null,
            0m,
            null));
    }
}
