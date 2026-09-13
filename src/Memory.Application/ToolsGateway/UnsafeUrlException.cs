namespace Memory.Application.ToolsGateway;

public sealed class UnsafeUrlException(string message) : ArgumentException(message);
