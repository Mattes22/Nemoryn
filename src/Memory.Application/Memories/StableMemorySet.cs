namespace Memory.Application.Memories;

public sealed record StableMemorySet(
    IReadOnlyList<RankedMemory> Core,
    IReadOnlyList<RankedMemory> Relevant)
{
    public IReadOnlyList<RankedMemory> All => Core.Concat(Relevant).ToArray();
}
