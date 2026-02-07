using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTipsAndInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowBehavioralInsights",
                table: "UserSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowEducationalTips",
                table: "UserSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSpendingInsights",
                table: "UserSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTipsAndInsights",
                table: "UserSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "EducationalTips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LocalizationKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalTips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureDisclaimers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Feature = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisclaimerText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalizationKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDisclaimers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAppUsageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAppUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAppUsageLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTipPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EducationalTipId = table.Column<int>(type: "int", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DismissedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTipPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTipPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTipPreferences_EducationalTips_EducationalTipId",
                        column: x => x.EducationalTipId,
                        principalTable: "EducationalTips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EducationalTips_Category",
                table: "EducationalTips",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_EducationalTips_DisplayOrder",
                table: "EducationalTips",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_EducationalTips_IsActive",
                table: "EducationalTips",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureDisclaimers_Feature",
                table: "FeatureDisclaimers",
                column: "Feature");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureDisclaimers_IsActive",
                table: "FeatureDisclaimers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserAppUsageLogs_ActivityType",
                table: "UserAppUsageLogs",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_UserAppUsageLogs_UserId",
                table: "UserAppUsageLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAppUsageLogs_UserId_OccurredAt",
                table: "UserAppUsageLogs",
                columns: new[] { "UserId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserTipPreferences_EducationalTipId",
                table: "UserTipPreferences",
                column: "EducationalTipId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTipPreferences_UserId",
                table: "UserTipPreferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTipPreferences_UserId_EducationalTipId",
                table: "UserTipPreferences",
                columns: new[] { "UserId", "EducationalTipId" },
                unique: true,
                filter: "[EducationalTipId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureDisclaimers");

            migrationBuilder.DropTable(
                name: "UserAppUsageLogs");

            migrationBuilder.DropTable(
                name: "UserTipPreferences");

            migrationBuilder.DropTable(
                name: "EducationalTips");

            migrationBuilder.DropColumn(
                name: "ShowBehavioralInsights",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "ShowEducationalTips",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "ShowSpendingInsights",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "ShowTipsAndInsights",
                table: "UserSettings");
        }
    }
}
