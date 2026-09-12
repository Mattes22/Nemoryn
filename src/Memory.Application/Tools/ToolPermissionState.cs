namespace Memory.Application.Tools;

public sealed class ToolPermissionState
{
    public ToolPermissionState(
        ToolPermissionProfile profile,
        IReadOnlyList<ToolTrust> trusts,
        IReadOnlyList<ToolCapability> capabilities,
        int remainingNetworkInvokes)
    {
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Unknown tool permission profile.");
        }

        Profile = profile;
        Trusts = trusts ?? throw new ArgumentNullException(nameof(trusts));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        RemainingNetworkInvokes = remainingNetworkInvokes;
    }

    public ToolPermissionProfile Profile { get; }
    public IReadOnlyList<ToolTrust> Trusts { get; }
    public IReadOnlyList<ToolCapability> Capabilities { get; }
    public int RemainingNetworkInvokes { get; private set; }

    public bool TryConsumeNetwork()
    {
        if (RemainingNetworkInvokes <= 0)
        {
            return false;
        }

        RemainingNetworkInvokes--;
        return true;
    }
}
