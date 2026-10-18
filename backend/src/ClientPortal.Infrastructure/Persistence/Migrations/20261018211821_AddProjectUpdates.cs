using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SharedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_updates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_updates_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_updates_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_updates_ProjectId",
                table: "project_updates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_updates_WorkspaceId_ProjectId",
                table: "project_updates",
                columns: new[] { "WorkspaceId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_updates");
        }
    }
}
