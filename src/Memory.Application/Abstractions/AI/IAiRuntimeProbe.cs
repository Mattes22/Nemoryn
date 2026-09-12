namespace Memory.Application.Abstractions.AI;

public interface IAiRuntimeProbe
{
    Task<AiRuntimeProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}

public sealed record AiRuntimeProbeResult(
    bool Reachable,
    bool Healthy,
    int? LatencyMs,
    int? StatusCode,
    string? Error);
