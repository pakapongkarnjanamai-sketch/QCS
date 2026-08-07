using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueRenewalPredecessorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT [RenewedFromRequestId]
                    FROM [Requests]
                    WHERE [RenewedFromRequestId] IS NOT NULL
                    GROUP BY [RenewedFromRequestId]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Cannot apply unique index IX_Requests_RenewedFromRequestId because duplicate non-null RenewedFromRequestId values exist.', 1;
                END
            ");

            migrationBuilder.DropIndex(
                name: "IX_Requests_RenewedFromRequestId",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RenewedFromRequestId",
                table: "Requests",
                column: "RenewedFromRequestId",
                unique: true,
                filter: "[RenewedFromRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_RenewedFromRequestId",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RenewedFromRequestId",
                table: "Requests",
                column: "RenewedFromRequestId");
        }
    }
}
