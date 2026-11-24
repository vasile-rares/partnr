using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partnr.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkspaceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliverableTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmailAttempts = table.Column<int>(type: "integer", nullable: false),
                    EmailNextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailLastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                name: "IX_notifications_RecipientUserId",
                table: "notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_WorkspaceId",
                table: "notifications",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
