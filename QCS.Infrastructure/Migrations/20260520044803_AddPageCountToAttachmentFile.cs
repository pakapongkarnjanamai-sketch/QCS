using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPageCountToAttachmentFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "AttachmentFiles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "AttachmentFiles");
        }
    }
}
