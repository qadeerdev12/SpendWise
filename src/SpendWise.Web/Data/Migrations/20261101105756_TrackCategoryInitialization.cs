using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendWise.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackCategoryInitialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CategoriesInitialized",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoriesInitialized",
                table: "AspNetUsers");
        }
    }
}
