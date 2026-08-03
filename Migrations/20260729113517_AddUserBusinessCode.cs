using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBusinessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessCode",
                table: "AspNetUsers",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BusinessCode",
                table: "AspNetUsers",
                column: "BusinessCode",
                unique: true,
                filter: "[BusinessCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BusinessCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BusinessCode",
                table: "AspNetUsers");
        }
    }
}
