namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Memories;

internal sealed class MemoryEvidenceConfiguration : IEntityTypeConfiguration<MemoryEvidence>
{
    public void Configure(EntityTypeBuilder<MemoryEvidence> builder)
    {
        builder.ToTable("memory_evidence");

        builder.HasKey(evidence => evidence.Id);

        builder.Property(evidence => evidence.Id)
            .HasColumnName("id");

        builder.Property(evidence => evidence.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();

        builder.Property(evidence => evidence.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(evidence => evidence.SourceMessageId)
            .HasColumnName("source_message_id")
            .IsRequired();

        builder.Property(evidence => evidence.SourceSummary)
            .HasColumnName("source_summary")
            .HasMaxLength(1000);

        builder.Property(evidence => evidence.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(evidence => evidence.CandidateId);
        builder.HasIndex(evidence => evidence.SourceMessageId);

        builder.HasIndex(evidence => new { evidence.CandidateId, evidence.SourceMessageId })
            .IsUnique();

        builder.HasOne(evidence => evidence.Candidate)
            .WithMany()
            .HasForeignKey(evidence => evidence.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(evidence => evidence.Conversation)
            .WithMany()
            .HasForeignKey(evidence => evidence.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(evidence => evidence.SourceMessage)
            .WithMany()
            .HasForeignKey(evidence => evidence.SourceMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
