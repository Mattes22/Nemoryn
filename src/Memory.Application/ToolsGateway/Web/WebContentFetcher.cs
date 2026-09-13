namespace Memory.Application.ToolsGateway.Web;

using System.Net;
using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.ToolsGateway;

internal sealed class WebContentFetcher(
    IHostAddressResolver addressResolver,
    IRawHttpFetcher httpFetcher,
    IOptionsMonitor<ToolsOptions> options) : IWebContentFetcher
{
    public async Task<WebPageContent> FetchAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var fetch = options.CurrentValue.Web.Fetch;
        var current = uri;
        PublicNetworkPolicy.EnsureFetchUri(current);

        for (var hop = 0; hop <= fetch.NormalizedMaxRedirects; hop++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsurePublicEndpointAsync(current, cancellationToken);

            var response = await httpFetcher.SendAsync(
                current,
                fetch.NormalizedMaxResponseBytes,
                cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                current = ResolveRedirect(current, response.Location);
                PublicNetworkPolicy.EnsureFetchUri(current);
                continue;
            }

            return HtmlContentCleaner.Clean(
                current.ToString(),
                response.StatusCode,
                response.ContentType,
                response.Body,
                fetch.NormalizedMaxTextChars);
        }

        throw new UnsafeUrlException("Too many HTTP redirects.");
    }

    private async Task EnsurePublicEndpointAsync(Uri uri, CancellationToken cancellationToken)
    {
        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await addressResolver.ResolveAsync(uri.IdnHost, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Host '{uri.IdnHost}' could not be resolved.", exception);
        }

        if (addresses.Count == 0)
        {
            throw new InvalidOperationException($"Host '{uri.IdnHost}' could not be resolved.");
        }

        if (addresses.Any(PublicNetworkPolicy.IsBlocked))
        {
            throw new UnsafeUrlException("URL is not allowed.");
        }
    }

    private static bool IsRedirect(int statusCode)
    {
        return statusCode is 301 or 302 or 303 or 307 or 308;
    }

    private static Uri ResolveRedirect(Uri current, string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new UnsafeUrlException("Redirect did not include a Location header.");
        }

        if (!Uri.TryCreate(current, location.Trim(), out var redirect))
        {
            throw new UnsafeUrlException("Redirect Location is not a valid URL.");
        }

        return redirect;
    }
}
