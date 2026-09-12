namespace Memory.Application.Configuration;

using Memory.Application.Tools;

public sealed class ExternalToolsOptions
{
    public const string SectionName = "ExternalTools";

    public List<ExternalToolDefinition> Tools { get; set; } = [];
}
