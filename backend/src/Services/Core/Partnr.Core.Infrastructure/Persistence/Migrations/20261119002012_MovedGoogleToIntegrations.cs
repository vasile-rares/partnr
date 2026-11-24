using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partnr.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovedGoogleToIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "google_connections");

            migrationBuilder.DropIndex(
                name: "IX_meetings_NextSyncAtUtc",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "GoogleEventId",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "NextSyncAtUtc",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "SyncAttempts",
                table: "meetings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleEventId",
                table: "meetings",
                type: "character varying(1024)",
                maxLength: 1024,
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

            migrationBuilder.CreateTable(
                name: "google_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GoogleEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_google_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_google_connections_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meetings_NextSyncAtUtc",
                table: "meetings",
                column: "NextSyncAtUtc",
                filter: "\"SyncStatus\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_google_connections_UserId",
                table: "google_connections",
                column: "UserId",
                unique: true);
        }
    }
}
