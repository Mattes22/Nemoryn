namespace Memory.Application.ToolsGateway;

public interface ICapabilityAuthorizer
{
    bool IsAuthorized(ITool tool, IReadOnlyCollection<string> granted);
}
