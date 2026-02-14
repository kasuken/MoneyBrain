using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class MakeEducationalTipIdRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTipPreferences_UserId_EducationalTipId",
                table: "UserTipPreferences");

            migrationBuilder.AlterColumn<int>(
                name: "EducationalTipId",
                table: "UserTipPreferences",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTipPreferences_UserId_EducationalTipId",
                table: "UserTipPreferences",
                columns: new[] { "UserId", "EducationalTipId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTipPreferences_UserId_EducationalTipId",
                table: "UserTipPreferences");

            migrationBuilder.AlterColumn<int>(
                name: "EducationalTipId",
                table: "UserTipPreferences",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_UserTipPreferences_UserId_EducationalTipId",
                table: "UserTipPreferences",
                columns: new[] { "UserId", "EducationalTipId" },
                unique: true,
                filter: "[EducationalTipId] IS NOT NULL");
        }
    }
}
