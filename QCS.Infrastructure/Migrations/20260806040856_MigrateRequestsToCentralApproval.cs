using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateRequestsToCentralApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CurrentStepId",
                table: "Requests",
                newName: "CurrentStepSequence");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentStepSequence",
                table: "Requests",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(
                """
                UPDATE [Requests]
                SET [Status] = CASE
                    WHEN [Status] = 2 THEN 5
                    WHEN [Status] = 9 THEN 3
                    ELSE [Status]
                END
                WHERE [Status] IN (2, 9);
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalDocumentId",
                table: "Requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalDocumentNumber",
                table: "Requests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepName",
                table: "Requests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusSyncedAt",
                table: "Requests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ApprovalDocumentId",
                table: "Requests",
                column: "ApprovalDocumentId",
                unique: true,
                filter: "[ApprovalDocumentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_ApprovalDocumentId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ApprovalDocumentId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ApprovalDocumentNumber",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "CurrentStepName",
                table: "Requests");

            // CurrentStepSequence is NOT dropped here. Up() reaches it by renaming CurrentStepId,
            // so the reverse is the rename at the end of this method - and a generated DropColumn
            // was sitting here, three statements before the Sql, AlterColumn and RenameColumn that
            // all address the same column. Down() could not have succeeded: it would have destroyed
            // the current-step data and then failed on "Invalid column name 'CurrentStepSequence'",
            // leaving the table with no current-step column at all. Nothing executes Down() in the
            // test suite, which is why a passing build said nothing about it.

            migrationBuilder.DropColumn(
                name: "StatusSyncedAt",
                table: "Requests");

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Requests] WHERE [Status] IN (2, 4, 6))
                    THROW 51000, 'Cannot roll back central approval statuses Returned, WaitingEffective, or Cancelled.', 1;

                UPDATE [Requests]
                SET [Status] = CASE
                    WHEN [Status] = 5 THEN 2
                    WHEN [Status] = 3 THEN 9
                    ELSE [Status]
                END
                WHERE [Status] IN (3, 5);

                UPDATE [Requests]
                SET [CurrentStepSequence] = 0
                WHERE [CurrentStepSequence] IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentStepSequence",
                table: "Requests",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "CurrentStepSequence",
                table: "Requests",
                newName: "CurrentStepId");
        }
    }
}
