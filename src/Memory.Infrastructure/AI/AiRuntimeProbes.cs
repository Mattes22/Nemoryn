namespace Memory.Infrastructure.AI;

using System.Diagnostics;
using Memory.Application.Abstractions.AI;

internal sealed class OllamaAiRuntimeProbe(IHttpClientFactory httpClientFactory) : IAiRuntimeProbe
{
    public Task<AiRuntimeProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        return AiRuntimeProbeHttp.GetAsync(httpClientFactory, "api/tags", cancellationToken);
    }
}

internal sealed class OpenAiAiRuntimeProbe(IHttpClientFactory httpClientFactory) : IAiRuntimeProbe
{
    public Task<AiRuntimeProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        return AiRuntimeProbeHttp.GetAsync(httpClientFactory, "models", cancellationToken);
    }
}

internal sealed class NullAiRuntimeProbe : IAiRuntimeProbe
{
    public Task<AiRuntimeProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AiRuntimeProbeResult(
            false,
            false,
            null,
            null,
            "AI provider is not configured."));
    }
}

internal static class AiRuntimeProbeHttp
{
    public static async Task<AiRuntimeProbeResult> GetAsync(
        IHttpClientFactory httpClientFactory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient("MemoryAiProbe");
            using var response = await client.GetAsync(relativePath, cancellationToken);
            var latencyMs = (int)started.ElapsedMilliseconds;
            if ((int)response.StatusCode is >= 200 and < 300)
            {
                return new AiRuntimeProbeResult(true, true, latencyMs, (int)response.StatusCode, null);
            }

            return new AiRuntimeProbeResult(
                true,
                false,
                latencyMs,
                (int)response.StatusCode,
                $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiRuntimeProbeResult(false, false, (int)started.ElapsedMilliseconds, null, "Timed out.");
        }
        catch (Exception exception)
        {
            return new AiRuntimeProbeResult(
                false,
                false,
                (int)started.ElapsedMilliseconds,
                null,
                exception.GetBaseException().Message);
        }
    }
}
