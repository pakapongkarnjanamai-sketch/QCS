using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateRequest2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DocumentTypeId",
                table: "Quotations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "Quotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "Quotations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "Quotations");
        }
    }
}
