using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Quotations",
                newName: "VendorId");

            migrationBuilder.AddColumn<int>(
                name: "AttachmentFileId",
                table: "Quotations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttachmentFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentFiles", x => x.Id);
                });

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

            migrationBuilder.DropTable(
                name: "AttachmentFiles");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_AttachmentFileId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AttachmentFileId",
                table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "VendorId",
                table: "Quotations",
                newName: "FilePath");
        }
    }
}
