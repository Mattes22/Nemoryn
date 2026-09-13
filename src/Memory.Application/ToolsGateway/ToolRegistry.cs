namespace Memory.Application.ToolsGateway;

internal sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyDictionary<string, ITool> _tools;
    private readonly ICapabilityAuthorizer _authorizer;

    public ToolRegistry(IEnumerable<ITool> tools, ICapabilityAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(authorizer);

        var map = new Dictionary<string, ITool>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            ArgumentNullException.ThrowIfNull(tool);
            var name = tool.Name?.Trim()
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
        _authorizer = authorizer;
        All = map.Values.ToArray();
    }

    public IReadOnlyList<ITool> All { get; }

    public ITool? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _tools.TryGetValue(name.Trim(), out var tool) ? tool : null;
    }

    public IReadOnlyList<ITool> ListAvailable(IReadOnlyCollection<string> capabilities)
    {
        return All.Where(tool => _authorizer.IsAuthorized(tool, capabilities)).ToArray();
    }

    public IReadOnlyList<ToolDescriptor> DescribeAvailable(IReadOnlyCollection<string> capabilities)
    {
        return ListAvailable(capabilities)
            .Select(tool => new ToolDescriptor(
                tool.Name,
                tool.Description,
                tool.RequiredCapabilities.ToArray(),
                tool.Parameters))
            .ToArray();
    }
}
