using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueMemoryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_memory_evidence_candidate_id_source_message_id",
                table: "memory_evidence",
                columns: new[] { "candidate_id", "source_message_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memory_evidence_candidate_id_source_message_id",
                table: "memory_evidence");
        }
    }
}
