namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Conversations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Id)
            .HasColumnName("id");

        builder.Property(conversation => conversation.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(conversation => conversation.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(conversation => conversation.Title)
            .HasColumnName("title")
            .HasMaxLength(500);

        builder.Property(conversation => conversation.LastMessageSequenceNumber)
            .HasColumnName("last_message_sequence_number")
            .IsRequired();

        builder.Property(conversation => conversation.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(conversation => conversation.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(conversation => new { conversation.OwnerId, conversation.ExternalId })
            .IsUnique();
        builder.HasIndex(conversation => conversation.OwnerId);
    }
}
