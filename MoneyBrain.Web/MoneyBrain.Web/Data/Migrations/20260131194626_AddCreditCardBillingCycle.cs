using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCardBillingCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCycleMonth",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditCardBillingSourceAccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingCycleDay",
                table: "Accounts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastBillingCycleDate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedPaymentAccountId",
                table: "Accounts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreditCardBillingSourceAccountId",
                table: "Transactions",
                column: "CreditCardBillingSourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_BillingCycleDay",
                table: "Accounts",
                column: "BillingCycleDay");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_LinkedPaymentAccountId",
                table: "Accounts",
                column: "LinkedPaymentAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Accounts_LinkedPaymentAccountId",
                table: "Accounts",
                column: "LinkedPaymentAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_CreditCardBillingSourceAccountId",
                table: "Transactions",
                column: "CreditCardBillingSourceAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Accounts_LinkedPaymentAccountId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_CreditCardBillingSourceAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreditCardBillingSourceAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_BillingCycleDay",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_LinkedPaymentAccountId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BillingCycleMonth",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreditCardBillingSourceAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BillingCycleDay",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LastBillingCycleDate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LinkedPaymentAccountId",
                table: "Accounts");
        }
    }
}
