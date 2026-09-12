namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Memory.Application.Abstractions.AI;

internal sealed class OpenAiAiModelCatalog(IHttpClientFactory httpClientFactory) : IAiModelCatalog
{
    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("MemoryAiProbe");
            using var response = await client.GetAsync("models", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenAiModelList>(
                SerializerOptions,
                cancellationToken);
            return payload?.Data?
                .Select(model => model.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed record OpenAiModelList(IReadOnlyList<OpenAiModel>? Data);

    private sealed record OpenAiModel([property: JsonPropertyName("id")] string? Id);
}
