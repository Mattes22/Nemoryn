using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memory_conflicts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conflicting_memory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_conflicts", x => x.id);
                    table.CheckConstraint("ck_memory_conflicts_confidence_range", "confidence >= 0 AND confidence <= 1");
                    table.ForeignKey(
                        name: "FK_memory_conflicts_memories_conflicting_memory_id",
                        column: x => x.conflicting_memory_id,
                        principalTable: "memories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_conflicts_memory_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "memory_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memory_conflicts_candidate_id_conflicting_memory_id",
                table: "memory_conflicts",
                columns: new[] { "candidate_id", "conflicting_memory_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_memory_conflicts_conflicting_memory_id",
                table: "memory_conflicts",
                column: "conflicting_memory_id");

            migrationBuilder.CreateIndex(
                name: "IX_memory_conflicts_conversation_id_status",
                table: "memory_conflicts",
                columns: new[] { "conversation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_conflicts_owner_id_status",
                table: "memory_conflicts",
                columns: new[] { "owner_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_conflicts");
        }
    }
}
