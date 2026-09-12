namespace Memory.Api.OpenAi;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Memory.Application.Abstractions.AI;
using Memory.Application.Agent;
using Memory.Application.Exceptions;
using Memory.Application.OpenAiCompatible;

internal static class OpenAiEndpoints
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapOpenAiCompatibleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1")
            .AddEndpointFilter<OpenAiApiKeyFilter>();

        group.MapPost("/chat/completions", CompleteChatAsync)
            .WithName("OpenAiChatCompletions");

        group.MapGet("/models", ListModelsAsync)
            .WithName("OpenAiListModels");
    }

    private static async Task<IResult> CompleteChatAsync(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request,
        IOpenAiCompatibleChatService chatService,
        IChatCompletionProvider chatCompletionProvider,
        IChatModelResolver chatModelResolver,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Memory.Api.OpenAi");
        var userMessage = LastUserMessage(request.Messages);
        if (userMessage is null)
        {
            logger.LogInformation(
                "OpenAI POST /v1/chat/completions rejected: no user message. stream={Stream} model={Model}",
                request.Stream == true,
                request.Model);
            return OpenAiError(
                StatusCodes.Status400BadRequest,
                "At least one non-empty user message is required.",
                "invalid_request_error");
        }

        var ownerId = OpenAiCompatibleIdentity.ResolveOwnerId(
            Header(httpContext, "X-OpenWebUI-User-Id"),
            Header(httpContext, "X-OpenWebUI-User-Email"),
            Header(httpContext, "X-OpenWebUI-User-Name"),
            Header(httpContext, "X-User-Id"),
            request.Metadata?.OwnerId,
            request.User);

        var conversationKey = OpenAiCompatibleIdentity.ResolveConversationKey(
            ownerId,
            Header(httpContext, "X-OpenWebUI-Chat-Id"),
            Header(httpContext, "X-Chat-Id"),
            request.Metadata?.ChatId,
            request.Metadata?.ConversationId,
            request.ConversationId);

        var sideTask = OpenAiCompatibleSideTask.Detect(request.Metadata?.Task, userMessage);
        logger.LogInformation(
            "OpenAI POST /v1/chat/completions stream={Stream} task={Task} passthrough={Passthrough} owner={OwnerId} conversation={ConversationKey} openWebUiUserId={HasUserId} openWebUiChatId={HasChatId} userMessageChars={Chars}",
            request.Stream == true,
            sideTask,
            sideTask is not null,
            ownerId,
            conversationKey,
            !string.IsNullOrWhiteSpace(Header(httpContext, "X-OpenWebUI-User-Id")),
            !string.IsNullOrWhiteSpace(Header(httpContext, "X-OpenWebUI-Chat-Id")),
            userMessage.Length);

        string chatModel;
        try
        {
            chatModel = await chatModelResolver.ResolveChatModelAsync(request.Model, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return OpenAiError(StatusCodes.Status400BadRequest, exception.Message, "invalid_request_error");
        }

        if (sideTask is not null)
        {
            return await PassthroughAsync(
                httpContext,
                request,
                chatCompletionProvider,
                chatModel,
                cancellationToken);
        }

        var command = new OpenAiCompatibleChatCommand(
            ownerId,
            conversationKey,
            userMessage,
            MemoryLimit: 8,
            RecentMessageCount: 12,
            ChatModel: chatModel);

        if (request.Stream == true)
        {
            return StreamChat(httpContext, request, command, chatService);
        }

        try
        {
            var result = await chatService.CompleteAsync(command, cancellationToken);
            return Results.Ok(ToResponse(request, result.Chat));
        }
        catch (NotFoundException exception)
        {
            return OpenAiError(StatusCodes.Status404NotFound, exception.Message, "invalid_request_error");
        }
        catch (ConflictException exception)
        {
            return OpenAiError(StatusCodes.Status409Conflict, exception.Message, "invalid_request_error");
        }
        catch (ArgumentException exception)
        {
            return OpenAiError(StatusCodes.Status400BadRequest, exception.Message, "invalid_request_error");
        }
    }

    private static IResult StreamChat(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request,
        OpenAiCompatibleChatCommand command,
        IOpenAiCompatibleChatService chatService)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var model = FirstNonEmpty(command.ChatModel, request.Model) ?? "memory-agent";

        return Results.Stream(
            async stream =>
            {
                var cancellationToken = httpContext.RequestAborted;
                var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);

                var sentRole = false;
                AgentChatResponse? completed = null;
                string? streamError = null;
                try
                {
                    await foreach (var chunk in chatService.StreamAsync(command, cancellationToken))
                    {
                        if (!string.IsNullOrEmpty(chunk.Delta))
                        {
                            var delta = sentRole
                                ? new OpenAiChatDelta(Role: null, chunk.Delta)
                                : new OpenAiChatDelta("assistant", chunk.Delta);
                            sentRole = true;
                            await WriteSseAsync(
                                writer,
                                Chunk(completionId, created, model, delta, finishReason: null),
                                cancellationToken);
                        }

                        if (chunk.Completed is not null)
                        {
                            completed = chunk.Completed;
                            model = FirstNonEmpty(completed.Model, request.Model) ?? model;
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException
                    || !cancellationToken.IsCancellationRequested)
                {
                    streamError = exception is NotFoundException or ConflictException or ArgumentException
                        ? exception.Message
                        : $"Model did not return a usable response: {exception.Message}";
                }

                var succeeded = streamError is null
                    && string.Equals(completed?.Status, "Succeeded", StringComparison.Ordinal);
                if (!sentRole)
                {
                    var fallback = streamError
                        ?? completed?.ErrorMessage
                        ?? "The model did not return a response.";
                    await WriteSseAsync(
                        writer,
                        Chunk(completionId, created, model, new OpenAiChatDelta("assistant", fallback), null),
                        cancellationToken);
                }

                await WriteSseAsync(
                    writer,
                    Chunk(
                        completionId,
                        created,
                        model,
                        new OpenAiChatDelta(Role: null, Content: null),
                        succeeded ? "stop" : "error"),
                    cancellationToken);
                await writer.WriteAsync("data: [DONE]\n\n");
                await writer.FlushAsync(cancellationToken);
            },
            contentType: "text/event-stream");
    }

    private static async Task<IResult> PassthroughAsync(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request,
        IChatCompletionProvider chatCompletionProvider,
        string chatModel,
        CancellationToken cancellationToken)
    {
        if (request.Stream == true)
        {
            return PassthroughStream(httpContext, request, chatCompletionProvider, chatModel);
        }

        if (!chatCompletionProvider.IsAvailable)
        {
            return OpenAiError(
                StatusCodes.Status503ServiceUnavailable,
                "Chat completion provider is not configured.",
                "server_error");
        }

        var messages = ToProviderMessages(request.Messages);
        if (messages.Count == 0)
        {
            return OpenAiError(
                StatusCodes.Status400BadRequest,
                "At least one non-empty message is required.",
                "invalid_request_error");
        }

        try
        {
            var completion = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(messages, Model: chatModel),
                cancellationToken);
            var content = completion.Content;
            var model = FirstNonEmpty(chatModel, chatCompletionProvider.ModelName, request.Model) ?? "memory-agent";
            var completionTokens = EstimateTokenCount(content);
            return Results.Ok(new OpenAiChatCompletionResponse(
                Id: $"chatcmpl-{Guid.NewGuid():N}",
                Object: "chat.completion",
                Created: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model: model,
                Choices:
                [
                    new OpenAiChatChoice(0, new OpenAiChatMessage("assistant", content), "stop")
                ],
                Usage: new OpenAiUsage(0, completionTokens, completionTokens),
                Memory: new OpenAiMemoryMetadata(
                    "Passthrough",
                    chatCompletionProvider.ProviderName,
                    chatCompletionProvider.ModelName,
                    Guid.Empty,
                    Guid.Empty,
                    null,
                    0,
                    0,
                    null)));
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            return OpenAiError(
                StatusCodes.Status502BadGateway,
                $"Model did not return a usable response: {exception.Message}",
                "server_error");
        }
    }

    private static IResult PassthroughStream(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request,
        IChatCompletionProvider chatCompletionProvider,
        string chatModel)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var model = FirstNonEmpty(chatModel, chatCompletionProvider.ModelName, request.Model) ?? "memory-agent";
        var messages = ToProviderMessages(request.Messages);

        return Results.Stream(
            async stream =>
            {
                var cancellationToken = httpContext.RequestAborted;
                var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                var sentRole = false;
                var succeeded = false;
                string? streamError = null;
                try
                {
                    if (!chatCompletionProvider.IsAvailable)
                    {
                        streamError = "Chat completion provider is not configured.";
                    }
                    else if (messages.Count == 0)
                    {
                        streamError = "At least one non-empty message is required.";
                    }
                    else
                    {
                        await foreach (var delta in chatCompletionProvider.StreamAsync(
                            new ChatCompletionRequest(messages, Model: chatModel),
                            cancellationToken))
                        {
                            if (string.IsNullOrEmpty(delta))
                            {
                                continue;
                            }

                            var chunkDelta = sentRole
                                ? new OpenAiChatDelta(Role: null, delta)
                                : new OpenAiChatDelta("assistant", delta);
                            sentRole = true;
                            await WriteSseAsync(
                                writer,
                                Chunk(completionId, created, model, chunkDelta, finishReason: null),
                                cancellationToken);
                        }

                        succeeded = sentRole;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException
                    || !cancellationToken.IsCancellationRequested)
                {
                    streamError = exception.Message;
                }

                if (!sentRole)
                {
                    await WriteSseAsync(
                        writer,
                        Chunk(
                            completionId,
                            created,
                            model,
                            new OpenAiChatDelta("assistant", streamError ?? "The model did not return a response."),
                            null),
                        cancellationToken);
                }

                await WriteSseAsync(
                    writer,
                    Chunk(
                        completionId,
                        created,
                        model,
                        new OpenAiChatDelta(Role: null, Content: null),
                        succeeded ? "stop" : "error"),
                    cancellationToken);
                await writer.WriteAsync("data: [DONE]\n\n");
                await writer.FlushAsync(cancellationToken);
            },
            contentType: "text/event-stream");
    }

    private static async Task<IResult> ListModelsAsync(
        IChatModelResolver chatModelResolver,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var models = await chatModelResolver.ListChatModelsAsync(cancellationToken);
        if (models.Count == 0)
        {
            models = ["memory-agent"];
        }

        loggerFactory.CreateLogger("Memory.Api.OpenAi").LogInformation(
            "OpenAI GET /v1/models count={Count} model={Model}",
            models.Count,
            models[0]);

        return Results.Ok(new OpenAiModelListResponse(
            Object: "list",
            Data: models.Select(id => new OpenAiModelResponse(
                Id: id,
                Object: "model",
                Created: DateTimeOffset.UnixEpoch.ToUnixTimeSeconds(),
                OwnedBy: "memory")).ToArray()));
    }

    internal static string? LastUserMessage(IReadOnlyList<OpenAiChatMessage>? messages)
    {
        return messages?
            .LastOrDefault(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(message.Content))
            ?.Content
            ?.Trim();
    }

    private static IReadOnlyList<ChatCompletionMessage> ToProviderMessages(
        IReadOnlyList<OpenAiChatMessage>? messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return [];
        }

        return messages
            .Where(message =>
                !string.IsNullOrWhiteSpace(message.Role)
                && !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ChatCompletionMessage(message.Role.Trim(), message.Content!.Trim()))
            .ToArray();
    }

    internal static OpenAiChatCompletionResponse ToResponse(
        OpenAiChatCompletionRequest request,
        AgentChatResponse agentResponse)
    {
        var succeeded = string.Equals(agentResponse.Status, "Succeeded", StringComparison.Ordinal);
        var content = agentResponse.AssistantMessage?.Content
            ?? (succeeded ? string.Empty : agentResponse.ErrorMessage)
            ?? string.Empty;
        var model = FirstNonEmpty(agentResponse.Model, request.Model) ?? "memory-agent";
        var promptTokens = EstimateTokenCount(agentResponse.PreparedTurn.SystemPromptBlock)
            + agentResponse.PreparedTurn.RecentMessages.Sum(message => EstimateTokenCount(message.Content));
        var completionTokens = EstimateTokenCount(content);

        return new OpenAiChatCompletionResponse(
            Id: $"chatcmpl-{Guid.NewGuid():N}",
            Object: "chat.completion",
            Created: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model: model,
            Choices:
            [
                new OpenAiChatChoice(
                    Index: 0,
                    Message: new OpenAiChatMessage("assistant", content),
                    FinishReason: succeeded ? "stop" : "error")
            ],
            Usage: new OpenAiUsage(
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: promptTokens + completionTokens),
            Memory: new OpenAiMemoryMetadata(
                agentResponse.Status,
                agentResponse.Provider,
                agentResponse.Model,
                agentResponse.PreparedTurn.ConversationId,
                agentResponse.UserMessage.Id,
                agentResponse.AssistantMessage?.Id,
                agentResponse.PreparedTurn.CoreMemories.Count,
                agentResponse.PreparedTurn.RelevantMemories.Count,
                agentResponse.ErrorMessage));
    }

    private static OpenAiChatCompletionChunk Chunk(
        string id,
        long created,
        string model,
        OpenAiChatDelta delta,
        string? finishReason)
    {
        return new OpenAiChatCompletionChunk(
            id,
            "chat.completion.chunk",
            created,
            model,
            [new OpenAiChatStreamChoice(0, delta, finishReason)]);
    }

    private static async Task WriteSseAsync(
        StreamWriter writer,
        OpenAiChatCompletionChunk chunk,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(chunk, StreamJsonOptions);
        await writer.WriteAsync($"data: {json}\n\n");
        await writer.FlushAsync(cancellationToken);
    }

    private static IResult OpenAiError(int statusCode, string message, string type)
    {
        return Results.Json(
            new OpenAiErrorResponse(new OpenAiError(message, type)),
            statusCode: statusCode);
    }

    private static string? Header(HttpContext httpContext, string name)
    {
        return httpContext.Request.Headers.TryGetValue(name, out var value)
            ? value.ToString()
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }
}
