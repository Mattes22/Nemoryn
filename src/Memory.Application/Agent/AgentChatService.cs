namespace Memory.Application.Agent;

using System.Text;
using Memory.Application.Abstractions.AI;
using Memory.Application.Conversations;
using Memory.Application.Tools;
using Memory.Domain.Conversations;

internal sealed class AgentChatService(
    IConversationService conversationService,
    IAgentMemoryService agentMemoryService,
    IChatCompletionProvider chatCompletionProvider,
    IToolRegistry toolRegistry,
    IToolRuntime toolRuntime) : IAgentChatService
{
    internal const int MaxToolRounds = 3;

    public async Task<AgentChatResponse> ChatAsync(
        Guid conversationId,
        AgentChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var turn = await ExecuteTurnAsync(conversationId, request, cancellationToken);
        var userMessage = await conversationService.EnqueueUserMessageIngestionAsync(
            conversationId,
            turn.UserMessage.Id,
            cancellationToken);

        return turn with { UserMessage = userMessage };
    }

    public async IAsyncEnumerable<AgentChatStreamChunk> ChatStreamingAsync(
        Guid conversationId,
        AgentChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var turn = await ExecuteTurnAsync(conversationId, request, cancellationToken);
        if (turn.AssistantMessage is not null
            && !string.IsNullOrEmpty(turn.AssistantMessage.Content))
        {
            yield return new AgentChatStreamChunk(turn.AssistantMessage.Content, Completed: null);
        }

        var userMessage = await conversationService.EnqueueUserMessageIngestionAsync(
            conversationId,
            turn.UserMessage.Id,
            cancellationToken);

        yield return new AgentChatStreamChunk(Delta: null, turn with { UserMessage = userMessage });
    }

    private async Task<AgentChatResponse> ExecuteTurnAsync(
        Guid conversationId,
        AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message is required.", nameof(request));
        }

        var permissions = ToolPermissionCatalog.Create(request.PermissionProfile);
        var chatModel = string.IsNullOrWhiteSpace(request.ChatModel)
            ? chatCompletionProvider.ModelName
            : request.ChatModel.Trim();

        var userMessage = await conversationService.AddMessageAsync(
            conversationId,
            new AddMessageRequest(
                MessageRole.User,
                request.Message,
                ExternalId: null,
                OccurredAt: null,
                EnqueueIngestion: false),
            cancellationToken);

        var prepared = await agentMemoryService.PrepareTurnAsync(
            conversationId,
            new PrepareAgentTurnRequest(
                request.Message,
                request.MemoryLimit,
                request.RecentMessageCount),
            cancellationToken);

        if (!chatCompletionProvider.IsAvailable)
        {
            return Failed(
                userMessage,
                prepared,
                "Chat completion provider is not configured.",
                permissions.Profile,
                chatModel);
        }

        try
        {
            var completion = await CompleteWithToolsAsync(
                prepared,
                request.Message,
                permissions,
                chatModel,
                cancellationToken);
            if (completion.Error is not null)
            {
                return Failed(
                    userMessage,
                    prepared,
                    completion.Error,
                    permissions.Profile,
                    chatModel,
                    completion.Trace);
            }

            var assistantMessage = await conversationService.AddMessageAsync(
                conversationId,
                new AddMessageRequest(
                    MessageRole.Assistant,
                    completion.Content!,
                    ExternalId: null,
                    OccurredAt: null),
                cancellationToken);

            return new AgentChatResponse(
                "Succeeded",
                chatCompletionProvider.ProviderName,
                chatModel,
                userMessage,
                assistantMessage,
                prepared,
                ErrorMessage: null,
                completion.Trace,
                permissions.Profile);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException
                or TaskCanceledException
                or TimeoutException
                or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            return Failed(
                userMessage,
                prepared,
                $"Model did not return a usable response: {exception.Message}",
                permissions.Profile,
                chatModel);
        }
    }

    private async Task<ModelTurn> CompleteWithToolsAsync(
        PrepareAgentTurnResponse prepared,
        string userMessage,
        ToolPermissionState permissions,
        string chatModel,
        CancellationToken cancellationToken)
    {
        var messages = BuildProviderMessages(prepared, userMessage);
        var toolContext = ToolContext.ForAgentTurn(
            prepared.ConversationId,
            prepared.OwnerId,
            prepared.Policy,
            permissions);
        var advertised = AuthorizedTools(toolContext);
        if (advertised.Count > 0)
        {
            AppendToolPreamble(messages, advertised);
        }

        var traces = new List<AgentToolTrace>();
        for (var round = 0; round < MaxToolRounds; round++)
        {
            advertised = AuthorizedTools(toolContext);
            var completion = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(
                    messages,
                    advertised.Count > 0 ? advertised : null,
                    chatModel),
                cancellationToken);
            var calls = completion.ToolCalls ?? [];
            if (calls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(completion.Content))
                {
                    return new ModelTurn(null, traces, "Model did not return a usable response.");
                }

                return new ModelTurn(completion.Content, traces, null);
            }

            messages.Add(new ChatCompletionMessage(
                "assistant",
                completion.Content ?? string.Empty,
                ToolCalls: calls));

            foreach (var call in calls)
            {
                var result = await toolRuntime.InvokeAsync(call, toolContext, cancellationToken);
                traces.Add(new AgentToolTrace(
                    call.Id,
                    call.Name,
                    call.ArgumentsJson,
                    result.Ok,
                    result.Content));
                messages.Add(new ChatCompletionMessage(
                    "tool",
                    result.Content,
                    ToolCallId: result.CallId,
                    Name: result.Name));
            }
        }

        return new ModelTurn(null, traces, "Tool loop exceeded the maximum number of rounds.");
    }

    private IReadOnlyList<ToolDefinition> AuthorizedTools(ToolContext toolContext)
    {
        return toolRegistry.Definitions
            .Where(definition => ToolAuthorization.TryAuthorize(definition, toolContext, out _))
            .ToArray();
    }

    private AgentChatResponse Failed(
        MessageResponse userMessage,
        PrepareAgentTurnResponse prepared,
        string error,
        ToolPermissionProfile permissionProfile,
        string chatModel,
        IReadOnlyList<AgentToolTrace>? toolTrace = null)
    {
        return new AgentChatResponse(
            "Failed",
            chatCompletionProvider.ProviderName,
            chatModel,
            userMessage,
            AssistantMessage: null,
            prepared,
            error,
            toolTrace ?? [],
            permissionProfile);
    }

    private static List<ChatCompletionMessage> BuildProviderMessages(
        PrepareAgentTurnResponse prepared,
        string currentUserMessage)
    {
        var messages = new List<ChatCompletionMessage>();
        if (!string.IsNullOrWhiteSpace(prepared.SystemPromptBlock))
        {
            messages.Add(new ChatCompletionMessage("system", prepared.SystemPromptBlock));
        }

        foreach (var message in prepared.RecentMessages)
        {
            messages.Add(new ChatCompletionMessage(
                ToProviderRole(message.Role),
                message.Content));
        }

        var trimmed = currentUserMessage.Trim();
        var last = messages.LastOrDefault();
        if (last is null
            || last.Role != "user"
            || !string.Equals(last.Content, trimmed, StringComparison.Ordinal))
        {
            messages.Add(new ChatCompletionMessage("user", trimmed));
        }

        return messages;
    }

    private static void AppendToolPreamble(
        List<ChatCompletionMessage> messages,
        IReadOnlyList<ToolDefinition> tools)
    {
        var names = string.Join(", ", tools.Select(tool => tool.Name));
        var line = new StringBuilder()
            .Append("You can call registered tools when they help. Available: ")
            .Append(names)
            .Append(". Never invent a tool result.");

        var systemIndex = messages.FindIndex(message => message.Role == "system");
        if (systemIndex >= 0)
        {
            var system = messages[systemIndex];
            messages[systemIndex] = system with { Content = system.Content + "\n\n" + line.ToString() };
            return;
        }

        messages.Insert(0, new ChatCompletionMessage("system", line.ToString()));
    }

    private static string ToProviderRole(MessageRole role)
    {
        return role switch
        {
            MessageRole.User => "user",
            MessageRole.Assistant => "assistant",
            MessageRole.System => "system",
            MessageRole.Tool => "tool",
            _ => throw new ArgumentOutOfRangeException(nameof(role), "Unknown message role.")
        };
    }

    private sealed record ModelTurn(
        string? Content,
        IReadOnlyList<AgentToolTrace> Trace,
        string? Error);
}
