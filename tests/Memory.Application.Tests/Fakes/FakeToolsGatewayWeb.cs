namespace Memory.Application.Tests.Fakes;

using System.Net;
using Memory.Application.ToolsGateway.Web;

internal sealed class FakeHostAddressResolver : IHostAddressResolver
{
    public Dictionary<string, IReadOnlyList<IPAddress>> Map { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = host.Trim().Trim('[', ']');
        if (IPAddress.TryParse(value, out var address))
        {
            return Task.FromResult<IReadOnlyList<IPAddress>>([address]);
        }

        if (Map.TryGetValue(value, out var addresses))
        {
            return Task.FromResult(addresses);
        }

        throw new InvalidOperationException($"Host '{host}' was not mapped in the fake resolver.");
    }
}

internal sealed class ScriptedRawHttpFetcher : IRawHttpFetcher
{
    private readonly Queue<RawHttpResponse> _responses = new();

    public IList<Uri> Requests { get; } = [];

    public void Enqueue(RawHttpResponse response) => _responses.Enqueue(response);

    public Task<RawHttpResponse> SendAsync(
        Uri uri,
        int maxResponseBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(uri);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No scripted HTTP response for '{uri}'.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

internal sealed class FakeWebSearchProvider : IWebSearchProvider
{
    public string Name => "Fake";
    public string? LastQuery { get; private set; }
    public int LastMaxResults { get; private set; }
    public WebSearchResult Result { get; set; } = new("q", "Fake", []);
    public Exception? Exception { get; set; }

    public Task<WebSearchResult> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastQuery = query;
        LastMaxResults = maxResults;
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Result);
    }
}

internal sealed class FakeWebContentFetcher : IWebContentFetcher
{
    public Uri? LastUri { get; private set; }
    public WebPageContent Result { get; set; } =
        new("https://example.com", "Example", "text", 200, "text/html");
    public Exception? Exception { get; set; }

    public Task<WebPageContent> FetchAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastUri = uri;
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Result);
    }
}
