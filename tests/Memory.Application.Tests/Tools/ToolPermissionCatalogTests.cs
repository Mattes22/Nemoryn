namespace Memory.Application.Tests.Tools;

using Memory.Application.Tools;

public sealed class ToolPermissionCatalogTests
{
    [Fact]
    public void Safe_is_the_default_enabled_profile()
    {
        var safe = Assert.Single(ToolPermissionCatalog.Enabled, profile => profile.Id == ToolPermissionProfile.Safe);

        Assert.Equal(ToolPermissionProfile.Safe, ToolPermissionCatalog.Default);
        Assert.Equal(ToolPermissionProfile.Safe, ToolPermissionCatalog.Normalize(null));
        Assert.Equal([ToolTrust.Builtin], safe.Trusts);
        Assert.Equal(
            [ToolCapability.Clock, ToolCapability.MemoryRead, ToolCapability.WebSearch, ToolCapability.WebRead],
            safe.Capabilities);
        Assert.DoesNotContain(ToolCapability.Network, safe.Capabilities);
    }

    [Fact]
    public void NetworkOnce_is_enabled_and_grants_one_network_call()
    {
        var descriptor = Assert.Single(
            ToolPermissionCatalog.Enabled,
            profile => profile.Id == ToolPermissionProfile.NetworkOnce);
        var state = ToolPermissionCatalog.Create(ToolPermissionProfile.NetworkOnce);

        Assert.Contains(ToolTrust.Untrusted, descriptor.Trusts);
        Assert.Contains(ToolCapability.WebSearch, descriptor.Capabilities);
        Assert.Contains(ToolCapability.WebRead, descriptor.Capabilities);
        Assert.Contains(ToolCapability.Network, descriptor.Capabilities);
        Assert.Equal(1, state.RemainingNetworkInvokes);
        Assert.True(state.TryConsumeNetwork());
        Assert.Equal(0, state.RemainingNetworkInvokes);
        Assert.False(state.TryConsumeNetwork());
    }

    [Fact]
    public void Admin_is_listed_but_not_enabled()
    {
        var admin = Assert.Single(ToolPermissionCatalog.All, profile => profile.Id == ToolPermissionProfile.Admin);

        Assert.False(admin.Enabled);
        Assert.DoesNotContain(ToolPermissionCatalog.Enabled, profile => profile.Id == ToolPermissionProfile.Admin);

        var exception = Assert.Throws<ArgumentException>(() =>
            ToolPermissionCatalog.Create(ToolPermissionProfile.Admin));

        Assert.Contains("Admin", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not enabled", exception.Message, StringComparison.Ordinal);
    }
}
