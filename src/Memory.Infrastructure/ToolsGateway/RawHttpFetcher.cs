namespace Memory.Infrastructure.ToolsGateway;

using System.Net.Http.Headers;
using Memory.Application.ToolsGateway.Web;

internal sealed class RawHttpFetcher(IHttpClientFactory httpClientFactory) : IRawHttpFetcher
{
    public const string HttpClientName = "NemorynToolsWebFetch";
    public const string UserAgent = "Nemoryn-Tools/1.0";

    public async Task<RawHttpResponse> SendAsync(
        Uri uri,
        int maxResponseBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain", 0.9));

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var location = response.Headers.Location?.ToString()
            ?? (response.Content.Headers.TryGetValues("Content-Location", out var values)
                ? values.FirstOrDefault()
                : null);
        var contentType = response.Content.Headers.ContentType?.ToString();
        var body = await ReadLimitedAsync(response.Content, maxResponseBytes, cancellationToken);

        return new RawHttpResponse(
            (int)response.StatusCode,
            contentType,
            location,
            body.Buffer,
            body.Truncated);
    }

    private static async Task<(byte[] Buffer, bool Truncated)> ReadLimitedAsync(
        HttpContent content,
        int maxResponseBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(maxResponseBytes, 64 * 1024));
        var chunk = new byte[8192];
        var remaining = maxResponseBytes;
        var truncated = false;

        while (true)
        {
            var toRead = truncated ? chunk.Length : Math.Min(chunk.Length, remaining);
            if (toRead <= 0)
            {
                truncated = true;
                break;
            }

            var read = await stream.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (truncated)
            {
                continue;
            }

            buffer.Write(chunk, 0, read);
            remaining -= read;
            if (remaining <= 0)
            {
                truncated = await stream.ReadAsync(chunk.AsMemory(0, 1), cancellationToken) > 0;
                break;
            }
        }

        return (buffer.ToArray(), truncated);
    }
}
