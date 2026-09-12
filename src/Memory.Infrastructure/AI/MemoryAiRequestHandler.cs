namespace Memory.Infrastructure.AI;

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Memory.Application.Configuration;

internal sealed class MemoryAiRequestHandler(IOptionsMonitor<MemoryAiOptions> options) : DelegatingHandler
{
    internal const string PlaceholderBase = "http://memory.ai.invalid/";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var root = new Uri(MemoryAiBaseUrl.Normalize(current.BaseUrl) + "/", UriKind.Absolute);
        var relative = request.RequestUri is { IsAbsoluteUri: true } absolute
            ? absolute.PathAndQuery.TrimStart('/')
            : (request.RequestUri?.OriginalString ?? string.Empty).TrimStart('/');
        request.RequestUri = new Uri(root, relative);
        request.Headers.Authorization = string.IsNullOrWhiteSpace(current.ApiKey)
            ? null
            : new AuthenticationHeaderValue("Bearer", current.ApiKey.Trim());

        return base.SendAsync(request, cancellationToken);
    }
}
