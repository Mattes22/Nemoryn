namespace Memory.Application.ToolsGateway.Web;

public sealed record RawHttpResponse(
    int StatusCode,
    string? ContentType,
    string? Location,
    byte[] Body,
    bool Truncated);
