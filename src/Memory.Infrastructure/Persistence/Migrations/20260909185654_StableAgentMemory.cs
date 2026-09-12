using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StableAgentMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_pinned",
                table: "memories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "memories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE memories
                SET origin = 'Inferred'
                WHERE origin = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_memories_owner_id_is_pinned",
                table: "memories",
                columns: new[] { "owner_id", "is_pinned" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memories_owner_id_is_pinned",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "is_pinned",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "memories");
        }
    }
}
