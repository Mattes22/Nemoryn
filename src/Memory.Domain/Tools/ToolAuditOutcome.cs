namespace Memory.Domain.Tools;

public enum ToolAuditOutcome
{
    Succeeded = 1,
    Failed = 2,
    Denied = 3,
    Unknown = 4,
    Invalid = 5,
    TimedOut = 6
}
