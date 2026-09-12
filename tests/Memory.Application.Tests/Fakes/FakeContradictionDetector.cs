namespace Memory.Application.Tests.Fakes;

using Memory.Application.Abstractions.AI;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class FakeContradictionDetector : IContradictionDetector
{
    public bool IsAvailable { get; set; }

    public MemoryContradictionDecision Decision { get; set; } = new(
        MemoryContradictionRelation.Unrelated,
        null,
        0m,
        null);

    public Task<MemoryContradictionDecision> DetectAsync(
        ExtractedMemoryCandidate candidate,
        IReadOnlyList<MemoryEntity> existingMemories,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Decision);
    }
}
