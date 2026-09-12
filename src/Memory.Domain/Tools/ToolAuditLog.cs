namespace Memory.Domain.Tools;

public sealed class ToolAuditLog
{
    public const int MaxCallIdLength = 100;
    public const int MaxNameLength = 100;
    public const int MaxTrustLength = 50;
    public const int MaxCapabilitiesLength = 200;
    public const int MaxArgumentsLength = 4000;
    public const int MaxResultLength = 2000;
    public const int MaxErrorLength = 2000;

    private ToolAuditLog()
    {
    }

    public ToolAuditLog(
        string ownerId,
        string callId,
        string name,
        ToolAuditOutcome outcome,
        Guid? conversationId = null,
        string? argumentsJson = null,
        string? trust = null,
        IReadOnlyList<string>? capabilities = null,
        string? result = null,
        string? error = null,
        DateTimeOffset? occurredAt = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Unknown tool audit outcome.");
        }

        Id = Guid.NewGuid();
        OwnerId = ownerId.Trim();
        ConversationId = conversationId == Guid.Empty ? null : conversationId;
        CallId = TruncateRequired(callId, MaxCallIdLength);
        Name = TruncateRequired(name, MaxNameLength);
        ArgumentsJson = Truncate(argumentsJson, MaxArgumentsLength);
        Trust = Truncate(trust, MaxTrustLength);
        Capabilities = FormatCapabilities(capabilities);
        Outcome = outcome;
        Ok = outcome == ToolAuditOutcome.Succeeded;
        Invoked = outcome is ToolAuditOutcome.Succeeded
            or ToolAuditOutcome.Failed
            or ToolAuditOutcome.TimedOut;
        Result = Ok ? Truncate(result, MaxResultLength) : null;
        Error = Ok ? null : Truncate(error, MaxErrorLength);
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public Guid? ConversationId { get; private set; }
    public string CallId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ArgumentsJson { get; private set; }
    public string? Trust { get; private set; }
    public string Capabilities { get; private set; } = string.Empty;
    public bool Invoked { get; private set; }
    public bool Ok { get; private set; }
    public ToolAuditOutcome Outcome { get; private set; }
    public string? Result { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public IReadOnlyList<string> CapabilityNames =>
        string.IsNullOrEmpty(Capabilities)
            ? []
            : Capabilities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatCapabilities(IReadOnlyList<string>? capabilities)
    {
        if (capabilities is null || capabilities.Count == 0)
        {
            return string.Empty;
        }

        var names = capabilities
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return TruncateRequired(string.Join(',', names), MaxCapabilitiesLength);
    }

    private static string TruncateRequired(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
