using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMonthlyBudgetDefaultOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonthlyBudgets_CategoryId_Year_Month",
                table: "MonthlyBudgets");

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "MonthlyBudgets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Month",
                table: "MonthlyBudgets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "MonthlyBudgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_CategoryId_IsDefault",
                table: "MonthlyBudgets",
                columns: new[] { "CategoryId", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_CategoryId_Year_Month",
                table: "MonthlyBudgets",
                columns: new[] { "CategoryId", "Year", "Month" },
                unique: true,
                filter: "[IsDefault] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonthlyBudgets_CategoryId_IsDefault",
                table: "MonthlyBudgets");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyBudgets_CategoryId_Year_Month",
                table: "MonthlyBudgets");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "MonthlyBudgets");

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "MonthlyBudgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Month",
                table: "MonthlyBudgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_CategoryId_Year_Month",
                table: "MonthlyBudgets",
                columns: new[] { "CategoryId", "Year", "Month" },
                unique: true);
        }
    }
}
