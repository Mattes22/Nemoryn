namespace Memory.Application.Tools;

using System.Globalization;
using System.Text.Json;

internal sealed class GetTimeTool(TimeProvider timeProvider) : ITool
{
    public ToolDefinition Definition { get; } = new(
        "get_time",
        "Returns the current date and time. Optional IANA timezone, otherwise UTC.",
        [
            new ToolParameter(
                "timezone",
                "string",
                "IANA timezone like Europe/Prague. Defaults to UTC.",
                Required: false)
        ],
        ToolTrust.Builtin,
        [ToolCapability.Clock]);

    public Task<ToolResult> InvokeAsync(
        ToolCall call,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string? timezone = null;
        var argumentsJson = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson.Trim();
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("timezone", out var timezoneElement)
                && timezoneElement.ValueKind != JsonValueKind.Null
                && timezoneElement.ValueKind != JsonValueKind.Undefined)
            {
                if (timezoneElement.ValueKind != JsonValueKind.String)
                {
                    return Task.FromResult(Failed(call, "timezone must be a string."));
                }

                timezone = timezoneElement.GetString();
            }
        }
        catch (JsonException)
        {
            return Task.FromResult(Failed(call, "Invalid JSON arguments."));
        }

        var utc = timeProvider.GetUtcNow();
        TimeZoneInfo zone;
        if (string.IsNullOrWhiteSpace(timezone))
        {
            zone = TimeZoneInfo.Utc;
            timezone = "UTC";
        }
        else
        {
            var timezoneId = timezone.Trim();
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(timezoneId, out var found) || found is null)
            {
                return Task.FromResult(Failed(call, $"Unknown timezone '{timezoneId}'."));
            }

            zone = found;
            timezone = timezoneId;
        }

        var local = TimeZoneInfo.ConvertTime(utc, zone);
        var payload = new Dictionary<string, string>
        {
            ["utc"] = utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["timezone"] = timezone,
            ["local"] = local.ToString("o", CultureInfo.InvariantCulture)
        };

        return Task.FromResult(new ToolResult(
            call.Id,
            Definition.Name,
            true,
            JsonSerializer.Serialize(payload)));
    }

    private ToolResult Failed(ToolCall call, string message)
        => new(call.Id, Definition.Name, false, message);
}
