namespace Memory.Domain.Tests.Ingestion;

using Memory.Domain.Ingestion;

public sealed class MemoryIngestionJobTests
{
    [Fact]
    public void Failed_job_retries_until_max_attempts()
    {
        var job = new MemoryIngestionJob(Guid.NewGuid(), Guid.NewGuid());

        job.MarkProcessing();
        job.MarkFailed("first");
        Assert.Equal(IngestionJobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);

        job.MarkProcessing();
        job.MarkFailed("second");
        Assert.Equal(IngestionJobStatus.Pending, job.Status);

        job.MarkProcessing();
        job.MarkFailed("third");
        Assert.Equal(IngestionJobStatus.Failed, job.Status);
        Assert.Equal(3, job.AttemptCount);
    }
}
