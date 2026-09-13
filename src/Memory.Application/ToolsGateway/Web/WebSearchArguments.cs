namespace Memory.Application.ToolsGateway.Web;

public sealed record WebSearchArguments(string Query, int? MaxResults = null);
