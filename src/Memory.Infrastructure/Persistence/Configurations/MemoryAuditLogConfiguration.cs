namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Memories;

internal sealed class MemoryAuditLogConfiguration : IEntityTypeConfiguration<MemoryAuditLog>
{
    public void Configure(EntityTypeBuilder<MemoryAuditLog> builder)
    {
        builder.ToTable("memory_audit_logs");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id");

        builder.Property(entry => entry.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entry => entry.ConversationId)
            .HasColumnName("conversation_id");

        builder.Property(entry => entry.MemoryId)
            .HasColumnName("memory_id");

        builder.Property(entry => entry.CandidateId)
            .HasColumnName("candidate_id");

        builder.Property(entry => entry.ConflictId)
            .HasColumnName("conflict_id");

        builder.Property(entry => entry.Action)
            .HasColumnName("action")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.ActorKind)
            .HasColumnName("actor_kind")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.ActorId)
            .HasColumnName("actor_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entry => entry.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500);

        builder.Property(entry => entry.Details)
            .HasColumnName("details")
            .HasColumnType("text");

        builder.Property(entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(entry => new { entry.OwnerId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.MemoryId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.CandidateId, entry.OccurredAt });
    }
}
