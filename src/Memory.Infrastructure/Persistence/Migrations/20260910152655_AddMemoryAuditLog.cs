using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memory_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    memory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    conflict_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    details = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memory_audit_logs_candidate_id_occurred_at",
                table: "memory_audit_logs",
                columns: new[] { "candidate_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_audit_logs_memory_id_occurred_at",
                table: "memory_audit_logs",
                columns: new[] { "memory_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_audit_logs_owner_id_occurred_at",
                table: "memory_audit_logs",
                columns: new[] { "owner_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_audit_logs");
        }
    }
}
