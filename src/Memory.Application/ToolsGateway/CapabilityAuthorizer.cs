namespace Memory.Application.ToolsGateway;

internal sealed class CapabilityAuthorizer : ICapabilityAuthorizer
{
    public bool IsAuthorized(ITool tool, IReadOnlyCollection<string> granted)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var allowed = ToolCapabilities.NormalizeAll(granted);
        foreach (var required in tool.RequiredCapabilities)
        {
            var capability = ToolCapabilities.Normalize(required);
            if (capability.Length == 0)
            {
                continue;
            }

            if (!allowed.Contains(capability))
            {
                return false;
            }
        }

        return true;
    }
}
