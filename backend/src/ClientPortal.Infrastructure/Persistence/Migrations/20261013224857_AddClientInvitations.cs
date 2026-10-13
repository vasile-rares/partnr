using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LinkedAtUtc",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_invitations_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_invitations_users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_invitations_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_UserId",
                table: "clients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_WorkspaceId_UserId",
                table: "clients",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_invitations_InvitedByUserId",
                table: "client_invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_invitations_open_per_client",
                table: "client_invitations",
                column: "ClientId",
                unique: true,
                filter: "\"AcceptedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_client_invitations_TokenHash",
                table: "client_invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_invitations_WorkspaceId",
                table: "client_invitations",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_clients_users_UserId",
                table: "clients",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_users_UserId",
                table: "clients");

            migrationBuilder.DropTable(
                name: "client_invitations");

            migrationBuilder.DropIndex(
                name: "IX_clients_UserId",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "IX_clients_WorkspaceId_UserId",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "LinkedAtUtc",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "clients");
        }
    }
}
