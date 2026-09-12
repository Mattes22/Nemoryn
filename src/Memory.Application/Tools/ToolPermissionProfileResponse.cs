namespace Memory.Application.Tools;

public sealed record ToolPermissionProfileResponse(
    ToolPermissionProfile Id,
    string Label,
    string Description,
    bool Enabled,
    IReadOnlyList<ToolTrust> Trusts,
    IReadOnlyList<ToolCapability> Capabilities);
