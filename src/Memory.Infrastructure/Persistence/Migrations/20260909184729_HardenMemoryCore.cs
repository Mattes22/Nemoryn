using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenMemoryCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_conversations_external_id",
                table: "conversations");

            migrationBuilder.AddColumn<string>(
                name: "fingerprint",
                table: "memories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "owner_id",
                table: "memories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "memories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "owner_id",
                table: "conversations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE conversations
                SET owner_id = 'unknown'
                WHERE owner_id = '';

                UPDATE memories AS memory
                SET owner_id = conversation.owner_id,
                    scope = 'User',
                    fingerprint = encode(sha256(memory.id::text::bytea), 'hex')
                FROM conversations AS conversation
                WHERE memory.conversation_id = conversation.id;
                """);

            migrationBuilder.CreateTable(
                name: "memory_ingestion_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_ingestion_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_ingestion_jobs_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_ingestion_jobs_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memories_fingerprint",
                table: "memories",
                column: "fingerprint",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_memories_owner_id_scope_status",
                table: "memories",
                columns: new[] { "owner_id", "scope", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_owner_id",
                table: "conversations",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_owner_id_external_id",
                table: "conversations",
                columns: new[] { "owner_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_ingestion_jobs_conversation_id",
                table: "memory_ingestion_jobs",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_ingestion_jobs_message_id",
                table: "memory_ingestion_jobs",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_ingestion_jobs_status_created_at",
                table: "memory_ingestion_jobs",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_ingestion_jobs");

            migrationBuilder.DropIndex(
                name: "IX_memories_fingerprint",
                table: "memories");

            migrationBuilder.DropIndex(
                name: "IX_memories_owner_id_scope_status",
                table: "memories");

            migrationBuilder.DropIndex(
                name: "IX_conversations_owner_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_conversations_owner_id_external_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "fingerprint",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "conversations");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_external_id",
                table: "conversations",
                column: "external_id",
                unique: true);
        }
    }
}
