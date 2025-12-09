using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentFileInQuotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttachmentFileId",
                table: "Quotations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_AttachmentFileId",
                table: "Quotations",
                column: "AttachmentFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_AttachmentFiles_AttachmentFileId",
                table: "Quotations",
                column: "AttachmentFileId",
                principalTable: "AttachmentFiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_AttachmentFiles_AttachmentFileId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_AttachmentFileId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AttachmentFileId",
                table: "Quotations");
        }
    }
}
