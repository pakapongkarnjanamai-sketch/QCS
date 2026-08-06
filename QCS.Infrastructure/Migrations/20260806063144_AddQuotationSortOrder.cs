using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Quotations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                ;WITH [OrderedQuotations] AS
                (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER (
                            PARTITION BY [RequestId]
                            ORDER BY [DocumentTypeId], [Id]) AS [NewSortOrder]
                    FROM [Quotations]
                )
                UPDATE [Quotations]
                SET [SortOrder] = [OrderedQuotations].[NewSortOrder]
                FROM [Quotations]
                INNER JOIN [OrderedQuotations]
                    ON [OrderedQuotations].[Id] = [Quotations].[Id];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Quotations");
        }
    }
}
