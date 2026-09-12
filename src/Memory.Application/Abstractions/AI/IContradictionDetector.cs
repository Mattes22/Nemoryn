namespace Memory.Application.Abstractions.AI;

using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public interface IContradictionDetector
{
    bool IsAvailable { get; }

    Task<MemoryContradictionDecision> DetectAsync(
        ExtractedMemoryCandidate candidate,
        IReadOnlyList<MemoryEntity> existingMemories,
        CancellationToken cancellationToken = default);
}
