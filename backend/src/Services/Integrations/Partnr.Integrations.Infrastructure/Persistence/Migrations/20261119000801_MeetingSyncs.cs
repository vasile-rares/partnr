using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partnr.Integrations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeetingSyncs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meeting_syncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SyncAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GoogleEventId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MeetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_syncs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meeting_syncs_NextSyncAtUtc",
                table: "meeting_syncs",
                column: "NextSyncAtUtc",
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_meeting_syncs_OrganizerUserId",
                table: "meeting_syncs",
                column: "OrganizerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_syncs");
        }
    }
}
