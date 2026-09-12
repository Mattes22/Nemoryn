using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Memory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceAndEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                table: "memories",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_message_sequence_number",
                table: "conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE conversations AS conversation
                SET last_message_sequence_number = COALESCE((
                    SELECT MAX(message.sequence_number)
                    FROM messages AS message
                    WHERE message.conversation_id = conversation.id
                ), 0);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_memories_embedding",
                table: "memories",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memories_embedding",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "embedding",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "last_message_sequence_number",
                table: "conversations");
        }
    }
}
