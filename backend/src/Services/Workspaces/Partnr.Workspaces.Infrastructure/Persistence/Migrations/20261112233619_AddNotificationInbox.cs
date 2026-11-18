using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partnr.Workspaces.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_RecipientUserId",
                table: "notifications");

            migrationBuilder.AddColumn<DateTime>(
                name: "ObsoleteAtUtc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAtUtc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_inbox",
                table: "notifications",
                columns: new[] { "RecipientUserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_inbox",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "ObsoleteAtUtc",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "ReadAtUtc",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientUserId",
                table: "notifications",
                column: "RecipientUserId");
        }
    }
}
