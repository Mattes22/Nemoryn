namespace Memory.Application.ToolsGateway;

public enum ToolExecutionStatus
{
    Succeeded = 1,
    Denied = 2,
    NotFound = 3,
    InvalidArguments = 4,
    ForbiddenUrl = 5,
    ProviderError = 6,
    TimedOut = 7
}
