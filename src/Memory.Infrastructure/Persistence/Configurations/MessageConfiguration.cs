namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Conversations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id");

        builder.Property(message => message.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(message => message.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(200);

        builder.Property(message => message.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(message => message.SequenceNumber)
            .HasColumnName("sequence_number")
            .IsRequired();

        builder.Property(message => message.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(message => new { message.ConversationId, message.SequenceNumber })
            .IsUnique();

        builder.HasIndex(message => new { message.ConversationId, message.ExternalId })
            .IsUnique()
            .HasFilter("external_id IS NOT NULL");

        builder.HasOne(message => message.Conversation)
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
