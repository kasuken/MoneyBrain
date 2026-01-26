using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRecurrenceDate",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceFrequency",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceStartDate",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurringTemplateId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RecurringTemplateId",
                table: "Transactions",
                column: "RecurringTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_RecurringTemplateId",
                table: "Transactions",
                column: "RecurringTemplateId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_RecurringTemplateId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_RecurringTemplateId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "NextRecurrenceDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecurrenceFrequency",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecurrenceStartDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecurringTemplateId",
                table: "Transactions");
        }
    }
}
