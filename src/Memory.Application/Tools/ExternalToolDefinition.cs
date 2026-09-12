namespace Memory.Application.Tools;

public sealed class ExternalToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public List<ExternalToolParameter> Parameters { get; set; } = [];
    public List<string> Capabilities { get; set; } = [];
}

public sealed class ExternalToolParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
}
