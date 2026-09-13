namespace Memory.Infrastructure.ToolsGateway;

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.ToolsGateway.Web;

internal sealed class SearXngWebSearchProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ToolsOptions> options) : IWebSearchProvider
{
    public const string HttpClientName = "NemorynToolsSearXng";

    public string Name => SearXngSearchParser.ProviderName;

    public async Task<WebSearchResult> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("query is required.", nameof(query));
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        if (!ToolsSearXngBaseUrl.TryCreate(options.CurrentValue.Web.SearXNG.BaseUrl, out var baseAddress))
        {
            throw new InvalidOperationException("Tools:Web:SearXNG:BaseUrl is not configured.");
        }

        var path = $"search?q={Uri.EscapeDataString(query.Trim())}&format=json&pageno=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(RawHttpFetcher.UserAgent);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"SearXNG returned HTTP {(int)response.StatusCode}.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("SearXNG response was empty.");
        }

        return SearXngSearchParser.Parse(body, query.Trim(), maxResults);
    }
}
