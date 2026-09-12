namespace Memory.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Memory.Domain.Conversations;
using Memory.Domain.Ingestion;
using Memory.Domain.Tools;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MemoryEntity> Memories => Set<MemoryEntity>();
    public DbSet<global::Memory.Domain.Memories.MemoryCandidate> MemoryCandidates => Set<global::Memory.Domain.Memories.MemoryCandidate>();
    public DbSet<global::Memory.Domain.Memories.MemoryEvidence> MemoryEvidence => Set<global::Memory.Domain.Memories.MemoryEvidence>();
    public DbSet<global::Memory.Domain.Memories.MemoryConflict> MemoryConflicts => Set<global::Memory.Domain.Memories.MemoryConflict>();
    public DbSet<global::Memory.Domain.Memories.MemoryAuditLog> MemoryAuditLogs => Set<global::Memory.Domain.Memories.MemoryAuditLog>();
    public DbSet<ToolAuditLog> ToolAuditLogs => Set<ToolAuditLog>();
    public DbSet<MemoryIngestionJob> IngestionJobs => Set<MemoryIngestionJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MemoryDbContext).Assembly);
    }
}
