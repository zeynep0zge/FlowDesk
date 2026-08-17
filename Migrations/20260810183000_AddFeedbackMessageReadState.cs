using FlowDesk.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260810183000_AddFeedbackMessageReadState")]
public partial class AddFeedbackMessageReadState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsRead",
            table: "FeedbackMessages",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReadAt",
            table: "FeedbackMessages",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsRead",
            table: "FeedbackMessages");

        migrationBuilder.DropColumn(
            name: "ReadAt",
            table: "FeedbackMessages");
    }
}
