namespace Memory.Application.ToolsGateway;

public interface IToolRegistry
{
    IReadOnlyList<ITool> All { get; }

    ITool? Get(string name);

    IReadOnlyList<ITool> ListAvailable(IReadOnlyCollection<string> capabilities);

    IReadOnlyList<ToolDescriptor> DescribeAvailable(IReadOnlyCollection<string> capabilities);
}
