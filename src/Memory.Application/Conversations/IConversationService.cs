namespace Memory.Application.Conversations;

public interface IConversationService
{
    Task<ConversationResponse> CreateAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ConversationWriteResult> UpsertByExternalIdAsync(
        string externalId,
        UpsertConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<ConversationResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationResponse>> ListForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>> GetMessagesAsync(
        Guid conversationId,
        int? take = null,
        CancellationToken cancellationToken = default);

    Task<MessageResponse> AddMessageAsync(
        Guid conversationId,
        AddMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageResponse> EnqueueUserMessageIngestionAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);
}
