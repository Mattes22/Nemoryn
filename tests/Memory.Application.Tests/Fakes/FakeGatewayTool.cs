namespace Memory.Application.Tests.Fakes;

using System.Text.Json;
using Memory.Application.ToolsGateway;

internal sealed class FakeGatewayTool : ITool
{
    public FakeGatewayTool(string name, params string[] requiredCapabilities)
    {
        Name = name;
        RequiredCapabilities = requiredCapabilities;
        ExecutePayload = new Dictionary<string, bool> { ["ok"] = true };
    }

    public string Name { get; }
    public string Description { get; init; } = "Fake gateway tool.";
    public IReadOnlyCollection<string> RequiredCapabilities { get; }
    public IReadOnlyList<ToolParameterDescriptor> Parameters { get; init; } = [];
    public object ExecutePayload { get; set; }
    public Exception? ExecuteException { get; set; }
    public bool Invoked { get; private set; }

    public Task<object> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Invoked = true;
        if (ExecuteException is not null)
        {
            throw ExecuteException;
        }

        return Task.FromResult(ExecutePayload);
    }
}

internal sealed class RecordingToolInvocationAuditor : IToolInvocationAuditor
{
    public IList<ToolInvocationAudit> Entries { get; } = [];

    public void Record(ToolInvocationAudit audit)
    {
        Entries.Add(audit);
    }
}
