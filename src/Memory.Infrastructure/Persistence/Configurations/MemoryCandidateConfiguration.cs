namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Memories;

internal sealed class MemoryCandidateConfiguration : IEntityTypeConfiguration<MemoryCandidate>
{
    public void Configure(EntityTypeBuilder<MemoryCandidate> builder)
    {
        builder.ToTable("memory_candidates");

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id)
            .HasColumnName("id");

        builder.Property(candidate => candidate.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(candidate => candidate.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(candidate => candidate.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(candidate => candidate.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(candidate => candidate.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(candidate => candidate.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(candidate => candidate.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(candidate => candidate.Importance)
            .HasColumnName("importance")
            .HasPrecision(3, 2)
            .IsRequired();

        builder.Property(candidate => candidate.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(3, 2)
            .IsRequired();

        builder.Property(candidate => candidate.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("timestamp with time zone");

        builder.Property(candidate => candidate.ValidUntil)
            .HasColumnName("valid_until")
            .HasColumnType("timestamp with time zone");

        builder.Property(candidate => candidate.EvidenceCount)
            .HasColumnName("evidence_count")
            .IsRequired();

        builder.Property(candidate => candidate.LastEvidenceAt)
            .HasColumnName("last_evidence_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(candidate => candidate.PromotedMemoryId)
            .HasColumnName("promoted_memory_id");

        builder.Property(candidate => candidate.MergedIntoCandidateId)
            .HasColumnName("merged_into_candidate_id");

        builder.Property(candidate => candidate.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(candidate => candidate.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(candidate => candidate.Fingerprint)
            .IsUnique()
            .HasFilter("status = 'Pending'");

        builder.HasIndex(candidate => new { candidate.OwnerId, candidate.Scope, candidate.Status });
        builder.HasIndex(candidate => new { candidate.Status, candidate.UpdatedAt });

        builder.HasOne<global::Memory.Domain.Conversations.Conversation>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<global::Memory.Domain.Memories.Memory>()
            .WithMany()
            .HasForeignKey(candidate => candidate.PromotedMemoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<MemoryCandidate>()
            .WithMany()
            .HasForeignKey(candidate => candidate.MergedIntoCandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_memory_candidates_importance_range",
                "importance >= 0 AND importance <= 1");

            tableBuilder.HasCheckConstraint(
                "ck_memory_candidates_confidence_range",
                "confidence >= 0 AND confidence <= 1");

            tableBuilder.HasCheckConstraint(
                "ck_memory_candidates_validity_range",
                "valid_until IS NULL OR valid_from IS NULL OR valid_until > valid_from");
        });
    }
}
