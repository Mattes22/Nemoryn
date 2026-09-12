namespace Memory.Application.Tools;

internal static class ToolAuthorization
{
    public static bool TryAuthorize(ToolDefinition definition, ToolContext context, out string reason)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        var allowedTrusts = context.ResolvedAllowedTrusts;
        if (!allowedTrusts.Contains(definition.Trust))
        {
            reason = $"Tool '{definition.Name}' is not allowed: trust {definition.Trust}.";
            return false;
        }

        if (definition.Capabilities.Contains(ToolCapability.Network)
            && !context.CanUseNetwork
            && context.Profile == ToolPermissionProfile.NetworkOnce)
        {
            reason = $"Tool '{definition.Name}' is not allowed: NetworkOnce already used.";
            return false;
        }

        var allowedCapabilities = context.EffectiveAllowedCapabilities;
        foreach (var capability in definition.Capabilities)
        {
            if (!allowedCapabilities.Contains(capability))
            {
                reason = $"Tool '{definition.Name}' is not allowed: capability {capability}.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }
}
