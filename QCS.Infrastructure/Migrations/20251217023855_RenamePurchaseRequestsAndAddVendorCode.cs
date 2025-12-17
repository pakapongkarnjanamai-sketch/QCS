using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePurchaseRequestsAndAddVendorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VendorCode",
                table: "Requests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VendorCode",
                table: "Requests");
        }
    }
}
