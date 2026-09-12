namespace Memory.Application.Memories;

public interface IMemoryBackupService
{
    Task<MemoryProfileExport> ExportAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<MemoryImportResult> ImportAsync(
        string ownerId,
        MemoryImportRequest request,
        CancellationToken cancellationToken = default);
}
