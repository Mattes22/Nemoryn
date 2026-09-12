namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Application.Configuration;
using MemoryEntity = Memory.Domain.Memories.Memory;

internal sealed class MemoryConfiguration : IEntityTypeConfiguration<MemoryEntity>
{
    public void Configure(EntityTypeBuilder<MemoryEntity> builder)
    {
        builder.ToTable("memories");

        builder.HasKey(memory => memory.Id);

        builder.Property(memory => memory.Id)
            .HasColumnName("id");

        builder.Property(memory => memory.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(memory => memory.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(memory => memory.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(memory => memory.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(memory => memory.Origin)
            .HasColumnName("origin")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(memory => memory.IsPinned)
            .HasColumnName("is_pinned")
            .IsRequired();

        builder.Property(memory => memory.SourceMessageId)
            .HasColumnName("source_message_id");

        builder.Property(memory => memory.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(memory => memory.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(memory => memory.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(memory => memory.Importance)
            .HasColumnName("importance")
            .HasPrecision(3, 2)
            .IsRequired();

        builder.Property(memory => memory.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(3, 2)
            .IsRequired();

        builder.Property(memory => memory.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("timestamp with time zone");

        builder.Property(memory => memory.ValidUntil)
            .HasColumnName("valid_until")
            .HasColumnType("timestamp with time zone");

        builder.Property(memory => memory.SourceSummary)
            .HasColumnName("source_summary")
            .HasMaxLength(1000);

        builder.Property(memory => memory.SourceMetadataJson)
            .HasColumnName("source_metadata")
            .HasColumnType("jsonb");

        builder.Property(memory => memory.SupersededByMemoryId)
            .HasColumnName("superseded_by_memory_id");

        builder.Property(memory => memory.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(memory => memory.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(memory => memory.Embedding)
            .HasColumnName("embedding")
            .HasColumnType($"vector({MemoryAiOptions.SupportedEmbeddingDimensions})")
            .HasConversion(
                embedding => embedding == null ? null : new Pgvector.Vector(embedding),
                vector => vector == null ? null : vector.ToArray(),
                new ValueComparer<float[]?>(
                    (left, right) =>
                        left == right
                        || (left != null && right != null && left.SequenceEqual(right)),
                    value => value == null
                        ? 0
                        : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    value => value == null ? null : value.ToArray()));

        builder.HasOne(memory => memory.Conversation)
            .WithMany()
            .HasForeignKey(memory => memory.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(memory => memory.SourceMessage)
            .WithMany()
            .HasForeignKey(memory => memory.SourceMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(memory => memory.SupersededByMemory)
            .WithMany()
            .HasForeignKey(memory => memory.SupersededByMemoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(memory => new { memory.Status, memory.Type });
        builder.HasIndex(memory => new { memory.OwnerId, memory.Scope, memory.Status });
        builder.HasIndex(memory => new { memory.OwnerId, memory.IsPinned });
        builder.HasIndex(memory => memory.ValidUntil);
        builder.HasIndex(memory => memory.SourceMessageId);
        builder.HasIndex(memory => memory.Fingerprint)
            .IsUnique()
            .HasFilter("status = 'Active'");
        builder.HasIndex(memory => memory.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_memories_importance_range",
                "importance >= 0 AND importance <= 1");

            tableBuilder.HasCheckConstraint(
                "ck_memories_confidence_range",
                "confidence >= 0 AND confidence <= 1");

            tableBuilder.HasCheckConstraint(
                "ck_memories_validity_range",
                "valid_until IS NULL OR valid_from IS NULL OR valid_until > valid_from");
        });
    }
}
