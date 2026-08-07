using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestIntentAndRenewalSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Intent",
                table: "Requests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RenewedFromRequestId",
                table: "Requests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RenewedFromRequestId",
                table: "Requests",
                column: "RenewedFromRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_Requests_RenewedFromRequestId",
                table: "Requests",
                column: "RenewedFromRequestId",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Requests_Requests_RenewedFromRequestId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_RenewedFromRequestId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Intent",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RenewedFromRequestId",
                table: "Requests");
        }
    }
}
