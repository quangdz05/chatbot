using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkEmbeddingStatusAndContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "Documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "DocumentChunks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingStatus",
                table: "DocumentChunks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "DocumentChunks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_ContentHash",
                table: "DocumentChunks",
                column: "ContentHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_ContentHash",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "DocumentChunks");
        }
    }
}
