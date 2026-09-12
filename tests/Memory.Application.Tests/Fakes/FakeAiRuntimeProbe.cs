namespace Memory.Application.Tests.Fakes;

using Memory.Application.Abstractions.AI;

internal sealed class FakeAiRuntimeProbe : IAiRuntimeProbe
{
    public AiRuntimeProbeResult Result { get; set; } = new(true, true, 12, 200, null);

    public Task<AiRuntimeProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result);
    }
}
