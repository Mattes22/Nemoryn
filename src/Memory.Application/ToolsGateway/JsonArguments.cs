namespace Memory.Application.ToolsGateway;

using System.Text.Json;

internal static class JsonArguments
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JsonElement ObjectOrEmpty(JsonElement arguments)
    {
        return arguments.ValueKind == JsonValueKind.Object
            ? arguments
            : JsonSerializer.SerializeToElement(new Dictionary<string, string>(), SerializerOptions);
    }

    public static T Deserialize<T>(JsonElement arguments)
    {
        var source = ObjectOrEmpty(arguments);
        return source.Deserialize<T>(SerializerOptions)
            ?? throw new ArgumentException("Arguments must be a JSON object.");
    }
}
