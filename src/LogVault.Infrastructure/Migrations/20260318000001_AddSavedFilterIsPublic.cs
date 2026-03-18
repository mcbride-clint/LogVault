using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedFilterIsPublic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "SavedFilters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "SavedFilters");
        }
    }
}
