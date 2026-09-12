namespace Memory.Application.Context;

public interface IContextBuilder
{
    Task<MemoryContextResponse> BuildAsync(
        Guid conversationId,
        BuildMemoryContextRequest request,
        CancellationToken cancellationToken = default);
}
