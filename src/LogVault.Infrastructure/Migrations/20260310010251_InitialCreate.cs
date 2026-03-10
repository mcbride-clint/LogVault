using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    FilterExpression = table.Column<string>(type: "TEXT", nullable: false),
                    MinimumLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceApplicationFilter = table.Column<string>(type: "TEXT", nullable: true),
                    ThrottleMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastFiredAt = table.Column<long>(type: "INTEGER", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultApplication = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RotatedFromId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_ApiKeys_RotatedFromId",
                        column: x => x.RotatedFromId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LogEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    RenderedMessage = table.Column<string>(type: "TEXT", nullable: false),
                    Exception = table.Column<string>(type: "TEXT", nullable: true),
                    SourceApplication = table.Column<string>(type: "TEXT", nullable: true),
                    SourceEnvironment = table.Column<string>(type: "TEXT", nullable: true),
                    SourceHost = table.Column<string>(type: "TEXT", nullable: true),
                    PropertiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    TraceId = table.Column<string>(type: "TEXT", nullable: true),
                    SpanId = table.Column<string>(type: "TEXT", nullable: true),
                    IngestedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlertRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRecipients_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertsFired",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlertRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggeringEventId = table.Column<long>(type: "INTEGER", nullable: false),
                    FiredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EmailSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertsFired", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertsFired_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRecipients_AlertRuleId",
                table: "AlertRecipients",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_OwnerId",
                table: "AlertRules",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertsFired_AlertRuleId",
                table: "AlertsFired",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertsFired_FiredAt",
                table: "AlertsFired",
                column: "FiredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_IsEnabled_IsRevoked_ExpiresAt",
                table: "ApiKeys",
                columns: new[] { "IsEnabled", "IsRevoked", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_RotatedFromId",
                table: "ApiKeys",
                column: "RotatedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_IngestedAt",
                table: "LogEvents",
                column: "IngestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_SourceApplication",
                table: "LogEvents",
                column: "SourceApplication");

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_Timestamp_Level",
                table: "LogEvents",
                columns: new[] { "Timestamp", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_TraceId",
                table: "LogEvents",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRecipients");

            migrationBuilder.DropTable(
                name: "AlertsFired");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "LogEvents");

            migrationBuilder.DropTable(
                name: "AlertRules");
        }
    }
}
