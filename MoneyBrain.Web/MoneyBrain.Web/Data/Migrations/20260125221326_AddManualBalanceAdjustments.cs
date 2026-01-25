using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBalanceAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualBalanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsReconciled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualBalanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualBalanceAdjustments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalanceAdjustments_AccountId",
                table: "ManualBalanceAdjustments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalanceAdjustments_AccountId_AdjustmentDate",
                table: "ManualBalanceAdjustments",
                columns: new[] { "AccountId", "AdjustmentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalanceAdjustments_AccountId_IsReconciled",
                table: "ManualBalanceAdjustments",
                columns: new[] { "AccountId", "IsReconciled" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalanceAdjustments_AdjustmentDate",
                table: "ManualBalanceAdjustments",
                column: "AdjustmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalanceAdjustments_Category",
                table: "ManualBalanceAdjustments",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualBalanceAdjustments");
        }
    }
}
