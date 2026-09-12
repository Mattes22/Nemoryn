namespace Memory.Application.Tests.Fakes;

using Memory.Application.Configuration;

internal sealed class FakeMemoryPolicyService(MemoryPolicyKind policy = MemoryPolicyKind.Balanced)
    : IMemoryPolicyService
{
    public MemoryPolicyKind Policy { get; set; } = policy;

    public MemoryPolicyResponse Get() => MemoryPolicyPresets.Describe(Policy);

    public MemoryPolicyResponse Set(MemoryPolicyKind next)
    {
        Policy = MemoryPolicyPresets.Normalize(next);
        return Get();
    }
}
