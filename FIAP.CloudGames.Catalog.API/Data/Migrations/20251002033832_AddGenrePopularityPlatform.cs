using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIAP.CloudGames.Catalog.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenrePopularityPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Genre",
                schema: "catalog",
                table: "Products",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                schema: "catalog",
                table: "Products",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Popularity",
                schema: "catalog",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Genre",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Platform",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Popularity",
                schema: "catalog",
                table: "Products");
        }
    }
}
