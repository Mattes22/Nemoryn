namespace Memory.Application.Tools;

using Memory.Application.Configuration;

public sealed record ToolContext(
    Guid ConversationId,
    string OwnerId,
    MemoryPolicyKind Policy,
    IReadOnlyList<ToolTrust>? AllowedTrusts = null,
    IReadOnlyList<ToolCapability>? AllowedCapabilities = null,
    ToolPermissionState? Permissions = null)
{
    public static IReadOnlyList<ToolTrust> DefaultAllowedTrusts { get; } = [ToolTrust.Builtin];

    public static IReadOnlyList<ToolCapability> DefaultAllowedCapabilities { get; } =
        [ToolCapability.Clock, ToolCapability.MemoryRead, ToolCapability.WebSearch, ToolCapability.WebRead];

    public static ToolContext None { get; } = new(Guid.Empty, string.Empty, MemoryPolicyKind.Balanced);

    public ToolPermissionProfile Profile => Permissions?.Profile ?? ToolPermissionCatalog.Default;

    public IReadOnlyList<ToolTrust> ResolvedAllowedTrusts =>
        Permissions?.Trusts ?? AllowedTrusts ?? DefaultAllowedTrusts;

    public IReadOnlyList<ToolCapability> ResolvedAllowedCapabilities =>
        Permissions?.Capabilities ?? AllowedCapabilities ?? DefaultAllowedCapabilities;

    public bool CanUseNetwork
    {
        get
        {
            if (!ResolvedAllowedCapabilities.Contains(ToolCapability.Network))
            {
                return false;
            }

            if (Permissions is null)
            {
                return true;
            }

            return Permissions.RemainingNetworkInvokes > 0;
        }
    }

    public IReadOnlyList<ToolCapability> EffectiveAllowedCapabilities
    {
        get
        {
            var allowed = ResolvedAllowedCapabilities;
            if (CanUseNetwork)
            {
                return allowed;
            }

            return allowed.Where(capability => capability != ToolCapability.Network).ToArray();
        }
    }

    public bool IsAvailable =>
        ConversationId != Guid.Empty && !string.IsNullOrWhiteSpace(OwnerId);

    public static ToolContext ForAgentTurn(
        Guid conversationId,
        string ownerId,
        MemoryPolicyKind policy,
        ToolPermissionProfile? profile = null)
    {
        return ForAgentTurn(
            conversationId,
            ownerId,
            policy,
            ToolPermissionCatalog.Create(profile));
    }

    public static ToolContext ForAgentTurn(
        Guid conversationId,
        string ownerId,
        MemoryPolicyKind policy,
        ToolPermissionState permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return new(
            conversationId,
            ownerId,
            policy,
            permissions.Trusts,
            permissions.Capabilities,
            permissions);
    }
}
