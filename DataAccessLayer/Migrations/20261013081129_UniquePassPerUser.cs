using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class UniquePassPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_passes_user_id",
                table: "passes");

            migrationBuilder.CreateIndex(
                name: "IX_passes_user_id",
                table: "passes",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_passes_user_id",
                table: "passes");

            migrationBuilder.CreateIndex(
                name: "IX_passes_user_id",
                table: "passes",
                column: "user_id");
        }
    }
}
