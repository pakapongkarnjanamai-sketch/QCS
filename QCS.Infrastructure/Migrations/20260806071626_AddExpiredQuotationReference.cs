using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpiredQuotationReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceQuotationId",
                table: "Quotations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_SourceQuotationId",
                table: "Quotations",
                column: "SourceQuotationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Quotations_SourceQuotationId",
                table: "Quotations",
                column: "SourceQuotationId",
                principalTable: "Quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Quotations_SourceQuotationId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_SourceQuotationId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "SourceQuotationId",
                table: "Quotations");
        }
    }
}
