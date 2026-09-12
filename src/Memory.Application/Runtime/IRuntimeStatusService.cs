namespace Memory.Application.Runtime;

public interface IRuntimeStatusService
{
    Task<RuntimeStatusResponse> GetAsync(CancellationToken cancellationToken = default);
}
