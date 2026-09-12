namespace Memory.Application.Memories;

public interface IMemoryRecallEvaluationService
{
    Task<MemoryRecallEvaluationResponse> EvaluateAsync(
        Guid conversationId,
        EvaluateMemoryRecallRequest request,
        CancellationToken cancellationToken = default);
}
