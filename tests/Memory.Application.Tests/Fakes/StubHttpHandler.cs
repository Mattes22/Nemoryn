namespace Memory.Application.Tests.Fakes;

using System.Net;

internal sealed class StubHttpHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = """{"ok":true}""";
    public bool Invoked { get; private set; }
    public int InvokeCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Invoked = true;
        InvokeCount++;
        LastRequest = request;
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(Status)
        {
            Content = new StringContent(ResponseBody)
        };
    }
}
