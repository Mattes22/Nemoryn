using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memory_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    importance = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_count = table.Column<int>(type: "integer", nullable: false),
                    last_evidence_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    promoted_memory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merged_into_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_candidates", x => x.id);
                    table.CheckConstraint("ck_memory_candidates_confidence_range", "confidence >= 0 AND confidence <= 1");
                    table.CheckConstraint("ck_memory_candidates_importance_range", "importance >= 0 AND importance <= 1");
                    table.CheckConstraint("ck_memory_candidates_validity_range", "valid_until IS NULL OR valid_from IS NULL OR valid_until > valid_from");
                    table.ForeignKey(
                        name: "FK_memory_candidates_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_candidates_memories_promoted_memory_id",
                        column: x => x.promoted_memory_id,
                        principalTable: "memories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_memory_candidates_memory_candidates_merged_into_candidate_id",
                        column: x => x.merged_into_candidate_id,
                        principalTable: "memory_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memory_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_memory_evidence_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_evidence_memory_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "memory_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_evidence_messages_source_message_id",
                        column: x => x.source_message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memory_candidates_conversation_id",
                table: "memory_candidates",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_candidates_fingerprint",
                table: "memory_candidates",
                column: "fingerprint",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_memory_candidates_merged_into_candidate_id",
                table: "memory_candidates",
                column: "merged_into_candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_candidates_owner_id_scope_status",
                table: "memory_candidates",
                columns: new[] { "owner_id", "scope", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_candidates_promoted_memory_id",
                table: "memory_candidates",
                column: "promoted_memory_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_candidates_status_updated_at",
                table: "memory_candidates",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_evidence_candidate_id",
                table: "memory_evidence",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_evidence_conversation_id",
                table: "memory_evidence",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_evidence_source_message_id",
                table: "memory_evidence",
                column: "source_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_evidence");

            migrationBuilder.DropTable(
                name: "memory_candidates");
        }
    }
}
