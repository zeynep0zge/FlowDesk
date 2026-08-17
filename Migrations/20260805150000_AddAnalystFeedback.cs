using FlowDesk.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260805150000_AddAnalystFeedback")]
public partial class AddAnalystFeedback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "AnalystFeedbackAt",
            table: "WorkItems",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AnalystFeedbackDescription",
            table: "WorkItems",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AnalystFeedbackStatus",
            table: "WorkItems",
            type: "int",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AnalystFeedbackAt",
            table: "WorkItems");

        migrationBuilder.DropColumn(
            name: "AnalystFeedbackDescription",
            table: "WorkItems");

        migrationBuilder.DropColumn(
            name: "AnalystFeedbackStatus",
            table: "WorkItems");
    }
}
