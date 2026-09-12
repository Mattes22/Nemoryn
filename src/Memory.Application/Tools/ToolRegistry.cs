namespace Memory.Application.Tools;

internal sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyDictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var map = new Dictionary<string, ITool>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            ArgumentNullException.ThrowIfNull(tool);
            var name = tool.Definition.Name?.Trim()
                ?? throw new InvalidOperationException("Tool name is required.");
            if (name.Length == 0)
            {
                throw new InvalidOperationException("Tool name is required.");
            }

            if (!map.TryAdd(name, tool))
            {
                throw new InvalidOperationException($"Duplicate tool name '{name}'.");
            }
        }

        _tools = map;
        All = map.Values.ToArray();
        Definitions = All.Select(tool => tool.Definition).ToArray();
    }

    public IReadOnlyList<ITool> All { get; }
    public IReadOnlyList<ToolDefinition> Definitions { get; }

    public ITool? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _tools.TryGetValue(name.Trim(), out var tool) ? tool : null;
    }
}
