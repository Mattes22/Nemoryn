using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddToolAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tool_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    call_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    arguments_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    trust = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    capabilities = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    invoked = table.Column<bool>(type: "boolean", nullable: false),
                    ok = table.Column<bool>(type: "boolean", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_audit_logs_conversation_id_occurred_at",
                table: "tool_audit_logs",
                columns: new[] { "conversation_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_audit_logs_name_occurred_at",
                table: "tool_audit_logs",
                columns: new[] { "name", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_audit_logs_owner_id_occurred_at",
                table: "tool_audit_logs",
                columns: new[] { "owner_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_audit_logs");
        }
    }
}
