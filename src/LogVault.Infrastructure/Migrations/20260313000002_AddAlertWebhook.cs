using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertWebhook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "AlertRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebhookFormat",
                table: "AlertRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "WebhookFormat",
                table: "AlertRules");
        }
    }
}
