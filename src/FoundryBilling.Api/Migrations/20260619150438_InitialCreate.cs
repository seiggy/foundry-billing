using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundryBilling.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoundryHubs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AzureResourceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SubscriptionId = table.Column<string>(type: "text", nullable: false),
                    ResourceGroup = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundryHubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FoundryProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HubId = table.Column<Guid>(type: "uuid", nullable: false),
                    AzureResourceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundryProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundryProjects_FoundryHubs_HubId",
                        column: x => x.HubId,
                        principalTable: "FoundryHubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HubId = table.Column<Guid>(type: "uuid", nullable: false),
                    AzureResourceId = table.Column<string>(type: "text", nullable: false),
                    DeploymentName = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: true),
                    SkuName = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDeployments_FoundryHubs_HubId",
                        column: x => x.HubId,
                        principalTable: "FoundryHubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyUsageRollups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: false),
                    CompletionTokens = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyUsageRollups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyUsageRollups_ModelDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "ModelDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageMetricSlices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: false),
                    CompletionTokens = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageMetricSlices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageMetricSlices_ModelDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "ModelDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyUsageRollups_Date_DeploymentId",
                table: "DailyUsageRollups",
                columns: new[] { "Date", "DeploymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyUsageRollups_DeploymentId",
                table: "DailyUsageRollups",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundryHubs_AzureResourceId",
                table: "FoundryHubs",
                column: "AzureResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoundryProjects_HubId",
                table: "FoundryProjects",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_AzureResourceId",
                table: "ModelDeployments",
                column: "AzureResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelDeployments_HubId",
                table: "ModelDeployments",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageMetricSlices_DeploymentId",
                table: "UsageMetricSlices",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageMetricSlices_Timestamp_DeploymentId",
                table: "UsageMetricSlices",
                columns: new[] { "Timestamp", "DeploymentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyUsageRollups");

            migrationBuilder.DropTable(
                name: "FoundryProjects");

            migrationBuilder.DropTable(
                name: "UsageMetricSlices");

            migrationBuilder.DropTable(
                name: "ModelDeployments");

            migrationBuilder.DropTable(
                name: "FoundryHubs");
        }
    }
}
