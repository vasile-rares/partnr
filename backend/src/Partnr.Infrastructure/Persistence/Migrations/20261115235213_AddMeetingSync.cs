using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partnr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Meetings that existed before the sync was never sent to Google: Failed lets a member send them with "Retry sync".
            migrationBuilder.AddColumn<string>(
                name: "GoogleEventId",
                table: "meetings",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetUrl",
                table: "meetings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextSyncAtUtc",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncAttempts",
                table: "meetings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SyncStatus",
                table: "meetings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Failed");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "meetings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_meetings_NextSyncAtUtc",
                table: "meetings",
                column: "NextSyncAtUtc",
                filter: "\"SyncStatus\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_meetings_NextSyncAtUtc",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "GoogleEventId",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "MeetUrl",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "NextSyncAtUtc",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "SyncAttempts",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "meetings");
        }
    }
}
