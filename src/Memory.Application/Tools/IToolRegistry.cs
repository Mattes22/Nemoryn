namespace Memory.Application.Tools;

public interface IToolRegistry
{
    IReadOnlyList<ITool> All { get; }
    IReadOnlyList<ToolDefinition> Definitions { get; }

    ITool? Get(string name);
}
