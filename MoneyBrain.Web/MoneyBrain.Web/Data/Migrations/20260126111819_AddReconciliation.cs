using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReconciliationId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Reconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    StatementDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatementBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReconciledBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    Difference = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reconciliations_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReconciliationId",
                table: "Transactions",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reconciliations_AccountId",
                table: "Reconciliations",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Reconciliations_ReconciliationId",
                table: "Transactions",
                column: "ReconciliationId",
                principalTable: "Reconciliations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Reconciliations_ReconciliationId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Reconciliations");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReconciliationId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReconciliationId",
                table: "Transactions");
        }
    }
}
