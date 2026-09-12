namespace Memory.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Memory.Domain.Tools;

internal sealed class ToolAuditLogConfiguration : IEntityTypeConfiguration<ToolAuditLog>
{
    public void Configure(EntityTypeBuilder<ToolAuditLog> builder)
    {
        builder.ToTable("tool_audit_logs");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id");

        builder.Property(entry => entry.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entry => entry.ConversationId)
            .HasColumnName("conversation_id");

        builder.Property(entry => entry.CallId)
            .HasColumnName("call_id")
            .HasMaxLength(ToolAuditLog.MaxCallIdLength)
            .IsRequired();

        builder.Property(entry => entry.Name)
            .HasColumnName("name")
            .HasMaxLength(ToolAuditLog.MaxNameLength)
            .IsRequired();

        builder.Property(entry => entry.ArgumentsJson)
            .HasColumnName("arguments_json")
            .HasMaxLength(ToolAuditLog.MaxArgumentsLength);

        builder.Property(entry => entry.Trust)
            .HasColumnName("trust")
            .HasMaxLength(ToolAuditLog.MaxTrustLength);

        builder.Property(entry => entry.Capabilities)
            .HasColumnName("capabilities")
            .HasMaxLength(ToolAuditLog.MaxCapabilitiesLength)
            .IsRequired();

        builder.Property(entry => entry.Invoked)
            .HasColumnName("invoked")
            .IsRequired();

        builder.Property(entry => entry.Ok)
            .HasColumnName("ok")
            .IsRequired();

        builder.Property(entry => entry.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.Result)
            .HasColumnName("result")
            .HasColumnType("text");

        builder.Property(entry => entry.Error)
            .HasColumnName("error")
            .HasColumnType("text");

        builder.Property(entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Ignore(entry => entry.CapabilityNames);

        builder.HasIndex(entry => new { entry.OwnerId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.ConversationId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.Name, entry.OccurredAt });
    }
}
