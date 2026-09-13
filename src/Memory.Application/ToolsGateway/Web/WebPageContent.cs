namespace Memory.Application.ToolsGateway.Web;

public sealed record WebPageContent(
    string Url,
    string? Title,
    string Text,
    int StatusCode,
    string? ContentType);
