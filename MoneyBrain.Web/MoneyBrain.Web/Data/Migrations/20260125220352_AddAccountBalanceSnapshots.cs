using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBalanceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountBalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalanceSnapshots_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalanceSnapshots_AccountId",
                table: "AccountBalanceSnapshots",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalanceSnapshots_AccountId_SnapshotDate",
                table: "AccountBalanceSnapshots",
                columns: new[] { "AccountId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalanceSnapshots_AccountId_Type",
                table: "AccountBalanceSnapshots",
                columns: new[] { "AccountId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalanceSnapshots_SnapshotDate",
                table: "AccountBalanceSnapshots",
                column: "SnapshotDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBalanceSnapshots");
        }
    }
}
