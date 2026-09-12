namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memory.Application.Abstractions.AI;

internal sealed class OllamaAiModelCatalog(IHttpClientFactory httpClientFactory) : IAiModelCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("MemoryAiProbe");
            using var response = await client.GetAsync("api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaTagList>(
                SerializerOptions,
                cancellationToken);
            return payload?.Models?
                .Select(model => model.Name ?? model.Model)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed record OllamaTagList(IReadOnlyList<OllamaTagModel>? Models);

    private sealed record OllamaTagModel(
        string? Name,
        [property: JsonPropertyName("model")] string? Model);
}
