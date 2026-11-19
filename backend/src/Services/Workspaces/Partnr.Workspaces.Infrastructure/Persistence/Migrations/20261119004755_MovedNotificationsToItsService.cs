using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partnr.Workspaces.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovedNotificationsToItsService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliverableTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmailAttempts = table.Column<int>(type: "integer", nullable: false),
                    EmailLastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmailNextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObsoleteAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_ApprovalRequestId",
                table: "notifications",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_email_due",
                table: "notifications",
                column: "EmailNextAttemptAtUtc",
                filter: "\"EmailStatus\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_inbox",
                table: "notifications",
                columns: new[] { "RecipientUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_WorkspaceId",
                table: "notifications",
                column: "WorkspaceId");
        }
    }
}
