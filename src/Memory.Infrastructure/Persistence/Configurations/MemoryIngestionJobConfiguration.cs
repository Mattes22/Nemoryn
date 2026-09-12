namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Ingestion;

internal sealed class MemoryIngestionJobConfiguration : IEntityTypeConfiguration<MemoryIngestionJob>
{
    public void Configure(EntityTypeBuilder<MemoryIngestionJob> builder)
    {
        builder.ToTable("memory_ingestion_jobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.Id)
            .HasColumnName("id");

        builder.Property(job => job.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(job => job.MessageId)
            .HasColumnName("message_id")
            .IsRequired();

        builder.Property(job => job.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(job => job.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(job => job.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType("timestamp with time zone");

        builder.Property(job => job.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(2000);

        builder.Property(job => job.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(job => job.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(job => job.MessageId)
            .IsUnique();

        builder.HasIndex(job => new { job.Status, job.CreatedAt });

        builder.HasOne<Memory.Domain.Conversations.Conversation>()
            .WithMany()
            .HasForeignKey(job => job.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Memory.Domain.Conversations.Message>()
            .WithMany()
            .HasForeignKey(job => job.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
