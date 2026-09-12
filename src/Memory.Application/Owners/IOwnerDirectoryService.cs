namespace Memory.Application.Owners;

public sealed record OwnerSummary(
    string OwnerId,
    int ConversationCount,
    int MemoryCount,
    DateTimeOffset? LastActivityAt);

public interface IOwnerDirectoryService
{
    Task<IReadOnlyList<OwnerSummary>> ListAsync(CancellationToken cancellationToken = default);
}
