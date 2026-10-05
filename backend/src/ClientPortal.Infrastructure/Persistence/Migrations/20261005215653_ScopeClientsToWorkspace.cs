using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeClientsToWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clients_Email",
                table: "clients");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_clients_WorkspaceId_Email",
                table: "clients",
                columns: new[] { "WorkspaceId", "Email" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_clients_workspaces_WorkspaceId",
                table: "clients",
                column: "WorkspaceId",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_workspaces_WorkspaceId",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "IX_clients_WorkspaceId_Email",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "clients");

            migrationBuilder.CreateIndex(
                name: "IX_clients_Email",
                table: "clients",
                column: "Email",
                unique: true);
        }
    }
}
