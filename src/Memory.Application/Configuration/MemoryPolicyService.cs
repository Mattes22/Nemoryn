namespace Memory.Application.Configuration;

using Microsoft.Extensions.Options;

internal sealed class MemoryPolicyService(
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions,
    IOptionsMonitorCache<MemoryAiOptions> cache,
    MemoryPolicyRuntime runtime) : IMemoryPolicyService
{
    public MemoryPolicyResponse Get()
    {
        return MemoryPolicyPresets.Describe(memoryAiOptions.CurrentValue);
    }

    public MemoryPolicyResponse Set(MemoryPolicyKind policy)
    {
        if (!Enum.IsDefined(policy) || policy == default)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Unknown memory policy.");
        }

        runtime.Override = policy;
        cache.TryRemove(Options.DefaultName);
        return Get();
    }
}
