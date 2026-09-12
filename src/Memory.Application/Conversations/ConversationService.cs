namespace Memory.Application.Conversations;

using Memory.Application.Abstractions.Persistence;
using Memory.Application.Exceptions;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;

internal sealed class ConversationService(IMemoryStore memoryStore) : IConversationService
{
    public async Task<ConversationResponse> CreateAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerAndExternalId(request.OwnerId, request.ExternalId);

        var existing = await memoryStore.GetConversationByExternalIdAsync(
            request.OwnerId,
            request.ExternalId,
            cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("Conversation with the same external id already exists.");
        }

        var conversation = new Conversation(request.OwnerId, request.ExternalId, request.Title);
        memoryStore.AddConversation(conversation);
        await memoryStore.SaveChangesAsync(cancellationToken);

        return ToResponse(conversation);
    }

    public async Task<ConversationWriteResult> UpsertByExternalIdAsync(
        string externalId,
        UpsertConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerAndExternalId(request.OwnerId, externalId);

        var conversation = await memoryStore.GetConversationByExternalIdAsync(
            request.OwnerId,
            externalId,
            cancellationToken);
        var created = conversation is null;

        if (conversation is null)
        {
            conversation = new Conversation(request.OwnerId, externalId, request.Title);
            memoryStore.AddConversation(conversation);
        }
        else
        {
            conversation.TryUpdateTitle(request.Title);
        }

        await memoryStore.SaveChangesAsync(cancellationToken);

        return new ConversationWriteResult(ToResponse(conversation), created);
    }

    public async Task<ConversationResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(id, cancellationToken);

        return conversation is null ? null : ToResponse(conversation);
    }

    public async Task<IReadOnlyList<ConversationResponse>> ListForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Conversation owner id is required.", nameof(ownerId));
        }

        var conversations = await memoryStore.GetConversationsForOwnerAsync(ownerId.Trim(), cancellationToken);
        return conversations.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>> GetMessagesAsync(
        Guid conversationId,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");

        var limit = take is > 0 ? Math.Min(take.Value, 200) : 80;
        var messages = await memoryStore.GetRecentMessagesAsync(conversation.Id, limit, cancellationToken);

        return messages
            .Select(message => new ConversationMessageResponse(
                message.Id,
                message.ConversationId,
                message.ExternalId,
                message.Role,
                message.Content,
                message.SequenceNumber,
                message.OccurredAt,
                message.CreatedAt))
            .ToArray();
    }

    public async Task<MessageResponse> AddMessageAsync(
        Guid conversationId,
        AddMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        EnumGuard.EnsureDefined(request.Role, nameof(request.Role));

        var (message, job) = await memoryStore.ExecuteInTransactionAsync(async () =>
        {
            var conversation = await memoryStore.GetConversationForUpdateAsync(conversationId, cancellationToken)
                ?? throw new NotFoundException("Conversation was not found.");

            var sequenceNumber = conversation.ReserveNextMessageSequence();
            var createdMessage = new Message(
                conversation.Id,
                request.Role,
                request.Content,
                sequenceNumber,
                request.ExternalId,
                request.OccurredAt);

            var ingestionJob = request.Role == MessageRole.User && request.EnqueueIngestion
                ? new MemoryIngestionJob(conversation.Id, createdMessage.Id)
                : null;

            memoryStore.AddMessage(createdMessage);
            if (ingestionJob is not null)
            {
                memoryStore.AddIngestionJob(ingestionJob);
            }

            await memoryStore.SaveChangesAsync(cancellationToken);

            return (createdMessage, ingestionJob);
        }, cancellationToken);

        return ToMessageResponse(message, job?.Id, job?.Status);
    }

    public async Task<MessageResponse> EnqueueUserMessageIngestionAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await memoryStore.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation was not found.");
        var message = await memoryStore.GetMessageAsync(messageId, cancellationToken)
            ?? throw new NotFoundException("Message was not found.");

        if (message.ConversationId != conversation.Id)
        {
            throw new ArgumentException("Message does not belong to the conversation.", nameof(messageId));
        }

        if (message.Role != MessageRole.User)
        {
            throw new ArgumentException("Only a user message can be ingested.", nameof(messageId));
        }

        var job = new MemoryIngestionJob(conversation.Id, message.Id);
        memoryStore.AddIngestionJob(job);
        await memoryStore.SaveChangesAsync(cancellationToken);

        return ToMessageResponse(message, job.Id, job.Status);
    }

    private static void ValidateOwnerAndExternalId(string ownerId, string externalId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Conversation owner id is required.", nameof(ownerId));
        }

        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ArgumentException("Conversation external id is required.", nameof(externalId));
        }
    }

    private static ConversationResponse ToResponse(Conversation conversation)
    {
        return new ConversationResponse(
            conversation.Id,
            conversation.OwnerId,
            conversation.ExternalId,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt);
    }

    private static MessageResponse ToMessageResponse(
        Message message,
        Guid? ingestionJobId,
        IngestionJobStatus? ingestionStatus)
    {
        return new MessageResponse(
            message.Id,
            message.ConversationId,
            message.ExternalId,
            message.Role,
            message.Content,
            message.SequenceNumber,
            message.OccurredAt,
            message.CreatedAt,
            ingestionJobId,
            ingestionStatus);
    }
}
