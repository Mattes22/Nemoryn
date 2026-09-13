namespace Memory.Application.Tools;

public static class ToolPermissionCatalog
{
    public static readonly ToolPermissionProfile Default = ToolPermissionProfile.Safe;

    public static IReadOnlyList<ToolPermissionProfileResponse> All { get; } =
    [
        Describe(
            ToolPermissionProfile.Safe,
            "Bezpečný",
            "Jen vestavěné nástroje: čas, čtení paměti a webové vyhledávání.",
            enabled: true,
            [ToolTrust.Builtin],
            [ToolCapability.Clock, ToolCapability.MemoryRead, ToolCapability.WebSearch, ToolCapability.WebRead]),
        Describe(
            ToolPermissionProfile.NetworkOnce,
            "Síť jednou",
            "Tento tah: jedno HTTP volání. OpenWebUI tento profil nedostane.",
            enabled: true,
            [ToolTrust.Builtin, ToolTrust.Untrusted],
            [
                ToolCapability.Clock,
                ToolCapability.MemoryRead,
                ToolCapability.WebSearch,
                ToolCapability.WebRead,
                ToolCapability.Network
            ]),
        Describe(
            ToolPermissionProfile.Admin,
            "Admin",
            "Souborový systém a shell. Zatím není povolený.",
            enabled: false,
            [ToolTrust.Builtin, ToolTrust.Untrusted],
            [
                ToolCapability.Clock,
                ToolCapability.MemoryRead,
                ToolCapability.WebSearch,
                ToolCapability.WebRead,
                ToolCapability.Network,
                ToolCapability.FileSystem,
                ToolCapability.Shell
            ])
    ];

    public static IReadOnlyList<ToolPermissionProfileResponse> Enabled =>
        All.Where(profile => profile.Enabled).ToArray();

    public static ToolPermissionProfile Normalize(ToolPermissionProfile? profile)
    {
        return profile ?? Default;
    }

    public static ToolPermissionState Create(ToolPermissionProfile? profile)
    {
        var resolved = Normalize(profile);
        var descriptor = All.FirstOrDefault(item => item.Id == resolved)
            ?? throw new ArgumentOutOfRangeException(nameof(profile), "Unknown tool permission profile.");

        if (!descriptor.Enabled)
        {
            throw new ArgumentException($"Tool permission profile '{resolved}' is not enabled.");
        }

        var remainingNetwork = resolved == ToolPermissionProfile.NetworkOnce ? 1 : 0;
        return new ToolPermissionState(
            descriptor.Id,
            descriptor.Trusts,
            descriptor.Capabilities,
            remainingNetwork);
    }

    private static ToolPermissionProfileResponse Describe(
        ToolPermissionProfile id,
        string label,
        string description,
        bool enabled,
        IReadOnlyList<ToolTrust> trusts,
        IReadOnlyList<ToolCapability> capabilities)
    {
        return new(id, label, description, enabled, trusts, capabilities);
    }
}
