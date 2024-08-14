using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceNet8.Migrations
{
    /// <inheritdoc />
    public partial class deneme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1372a768-421a-440d-ac9b-3297151b4fd7",
                column: "NormalizedName",
                value: "ADMİNİSTRATOR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1372a768-421a-440d-ac9b-3297151b4fd7",
                column: "NormalizedName",
                value: "ADMINISTRATOR");
        }
    }
}
