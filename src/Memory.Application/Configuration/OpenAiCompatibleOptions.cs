namespace Memory.Application.Configuration;

public sealed class OpenAiCompatibleOptions
{
    public const string SectionName = "OpenAiCompatible";

    public string? ApiKey { get; set; }
}
