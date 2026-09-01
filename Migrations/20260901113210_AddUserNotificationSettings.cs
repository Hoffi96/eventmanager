using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelferApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Notify1hBeforeTask",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Notify24hBeforeTask",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnAssignment",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notify1hBeforeTask",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Notify24hBeforeTask",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotifyOnAssignment",
                table: "Users");
        }
    }
}
