using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryGroupType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "CategoryGroups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Data migration: Set Type = 1 (Income) for groups with "income" in name
            migrationBuilder.Sql(
                @"UPDATE CategoryGroups 
                  SET Type = 1 
                  WHERE LOWER(Name) LIKE '%income%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "CategoryGroups");
        }
    }
}
