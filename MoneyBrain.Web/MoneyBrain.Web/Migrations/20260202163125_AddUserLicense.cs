using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLicense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLicenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PlanName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TrialStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubscriptionStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubscriptionEndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastValidatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLicenses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_Status",
                table: "UserLicenses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_StripeCustomerId",
                table: "UserLicenses",
                column: "StripeCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_StripeSubscriptionId",
                table: "UserLicenses",
                column: "StripeSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_UserId",
                table: "UserLicenses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLicenses");
        }
    }
}
