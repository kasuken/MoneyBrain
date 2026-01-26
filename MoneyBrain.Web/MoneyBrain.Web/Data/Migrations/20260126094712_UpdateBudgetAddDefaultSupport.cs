using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBudgetAddDefaultSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "Budgets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Month",
                table: "Budgets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Budgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Name_IsDefault",
                table: "Budgets",
                columns: new[] { "UserId", "Name", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Name_Year_Month",
                table: "Budgets",
                columns: new[] { "UserId", "Name", "Year", "Month" },
                unique: true,
                filter: "[IsDefault] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Name_IsDefault",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Name_Year_Month",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Budgets");

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "Budgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Month",
                table: "Budgets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
