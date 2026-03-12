using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedFilterPinned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "SavedFilters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "SavedFilters");
        }
    }
}
