using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelferApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettingsAndCoordinatorFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmailEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUser = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    EnableSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    FromAddress = table.Column<string>(type: "TEXT", nullable: true),
                    FromName = table.Column<string>(type: "TEXT", nullable: true),
                    RemindersEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reminder24h = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reminder1h = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");
        }
    }
}
