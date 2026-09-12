namespace Memory.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Configuration;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class OpenAiContradictionDetector(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<MemoryAiOptions> memoryAiOptions) : IContradictionDetector
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsAvailable => OpenAiAvailability.IsConfigured(memoryAiOptions.CurrentValue);

    public async Task<MemoryContradictionDecision> DetectAsync(
        ExtractedMemoryCandidate candidate,
        IReadOnlyList<MemoryEntity> existingMemories,
        CancellationToken cancellationToken = default)
    {
        if (existingMemories.Count == 0)
        {
            return ContradictionDecisionMapper.None();
        }

        var options = memoryAiOptions.CurrentValue;
        var client = httpClientFactory.CreateClient("MemoryAi");
        using var response = await client.PostAsJsonAsync(
            "chat/completions",
            new
            {
                model = options.ChatModel,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = ContradictionDecisionMapper.SystemPrompt },
                    new { role = "user", content = BuildPrompt(candidate, existingMemories) }
                }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Contradiction response was empty.");

        var content = payload.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return ContradictionDecisionMapper.None();
        }

        var parsed = JsonSerializer.Deserialize<ContradictionDecisionJson>(content, SerializerOptions);
        return ContradictionDecisionMapper.Map(parsed);
    }

    private static string BuildPrompt(ExtractedMemoryCandidate candidate, IReadOnlyList<MemoryEntity> existingMemories)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Proposed memory:");
        builder.AppendLine($"{candidate.Type}/{candidate.Scope}: {candidate.Content}");
        builder.AppendLine();
        builder.AppendLine("Existing active memories:");

        foreach (var memory in existingMemories)
        {
            builder.AppendLine($"{memory.Id}: {memory.Type}/{memory.Scope}: {memory.Content}");
        }

        return builder.ToString();
    }

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices);

    private sealed record ChatChoice(ChatMessage? Message);

    private sealed record ChatMessage(string? Content);
}
