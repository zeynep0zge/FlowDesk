using FlowDesk.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260810170000_AddFeedbackMessages")]
public partial class AddFeedbackMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FeedbackMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation(
                        "SqlServer:Identity",
                        "1, 1"),
                WorkItemId = table.Column<int>(
                    type: "int",
                    nullable: false),
                SenderUserId = table.Column<int>(
                    type: "int",
                    nullable: false),
                Message = table.Column<string>(
                    type: "nvarchar(2000)",
                    maxLength: 2000,
                    nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "datetime2",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FeedbackMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_FeedbackMessages_AspNetUsers_SenderUserId",
                    column: x => x.SenderUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FeedbackMessages_WorkItems_WorkItemId",
                    column: x => x.WorkItemId,
                    principalTable: "WorkItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FeedbackMessages_SenderUserId",
            table: "FeedbackMessages",
            column: "SenderUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FeedbackMessages_WorkItemId_CreatedAt",
            table: "FeedbackMessages",
            columns: new[] { "WorkItemId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FeedbackMessages");
    }
}
