using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Manager.Migrations
{
    /// <inheritdoc />
    public partial class AddUnicodeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_ArticleNumber_ColorId",
                table: "Products",
                columns: new[] { "ArticleNumber", "ColorId" },
                unique: true,
                filter: "[ColorId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_ArticleNumber_ColorId",
                table: "Products");
        }
    }
}
