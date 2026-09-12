namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Memories;

internal sealed class MemoryConflictConfiguration : IEntityTypeConfiguration<MemoryConflict>
{
    public void Configure(EntityTypeBuilder<MemoryConflict> builder)
    {
        builder.ToTable("memory_conflicts");

        builder.HasKey(conflict => conflict.Id);

        builder.Property(conflict => conflict.Id)
            .HasColumnName("id");

        builder.Property(conflict => conflict.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(conflict => conflict.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(conflict => conflict.CandidateId)
            .HasColumnName("candidate_id")
            .IsRequired();

        builder.Property(conflict => conflict.ConflictingMemoryId)
            .HasColumnName("conflicting_memory_id")
            .IsRequired();

        builder.Property(conflict => conflict.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(3, 2)
            .IsRequired();

        builder.Property(conflict => conflict.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1000);

        builder.Property(conflict => conflict.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(conflict => conflict.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(conflict => conflict.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(conflict => new { conflict.OwnerId, conflict.Status });
        builder.HasIndex(conflict => new { conflict.ConversationId, conflict.Status });

        builder.HasIndex(conflict => new { conflict.CandidateId, conflict.ConflictingMemoryId })
            .IsUnique()
            .HasFilter("status = 'Pending'");

        builder.HasOne(conflict => conflict.Candidate)
            .WithMany()
            .HasForeignKey(conflict => conflict.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(conflict => conflict.ConflictingMemory)
            .WithMany()
            .HasForeignKey(conflict => conflict.ConflictingMemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_memory_conflicts_confidence_range",
                "confidence >= 0 AND confidence <= 1");
        });
    }
}
