using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemAiDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemAiDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<int>(type: "int", nullable: false),
                    GeneratedRequest = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EditedRequest = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AbbreviationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmbiguitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnresolvedTermsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAiDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAiDrafts_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAiDrafts_WorkItemId",
                table: "WorkItemAiDrafts",
                column: "WorkItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemAiDrafts");
        }
    }
}
