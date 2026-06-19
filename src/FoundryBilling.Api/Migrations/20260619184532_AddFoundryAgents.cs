using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundryBilling.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFoundryAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentsDiscovered",
                table: "SyncRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FoundryAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundryAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundryAgents_FoundryProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "FoundryProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoundryAgents_ProjectId_AgentId",
                table: "FoundryAgents",
                columns: new[] { "ProjectId", "AgentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoundryAgents");

            migrationBuilder.DropColumn(
                name: "AgentsDiscovered",
                table: "SyncRuns");
        }
    }
}
