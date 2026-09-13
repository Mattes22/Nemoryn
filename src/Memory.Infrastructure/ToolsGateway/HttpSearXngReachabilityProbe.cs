namespace Memory.Infrastructure.ToolsGateway;

using System.Net.Http.Headers;
using Memory.Application.Configuration;
using Memory.Application.Runtime;
using Memory.Application.ToolsGateway.Web;

internal sealed class HttpSearXngReachabilityProbe(IHttpClientFactory httpClientFactory) : ISearXngReachabilityProbe
{
    public const string HttpClientName = "NemorynToolsSearXngProbe";

    public async Task<(bool Reachable, string? Error)> ProbeAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!ToolsSearXngBaseUrl.TryCreate(baseUrl, out var baseAddress))
        {
            return (false, null);
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(baseAddress, "search?q=nemoryn-probe&format=json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd(RawHttpFetcher.UserAgent);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            return (false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (false, exception.GetBaseException().Message);
        }
    }
}
