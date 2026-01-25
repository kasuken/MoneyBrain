using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalanceAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpeningBalanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    NewBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AdjustmentAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AdjustedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdjustedByUserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceAdjustments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceAdjustments_AccountId",
                table: "OpeningBalanceAdjustments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceAdjustments_AccountId_AdjustedAt",
                table: "OpeningBalanceAdjustments",
                columns: new[] { "AccountId", "AdjustedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceAdjustments_AdjustedAt",
                table: "OpeningBalanceAdjustments",
                column: "AdjustedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningBalanceAdjustments");
        }
    }
}
