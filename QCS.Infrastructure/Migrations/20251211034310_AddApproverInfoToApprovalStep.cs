using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApproverInfoToApprovalStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Role",
                table: "ApprovalSteps",
                newName: "StepName");

            migrationBuilder.AddColumn<string>(
                name: "ApproverNId",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverName",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApproverNId",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "ApproverName",
                table: "ApprovalSteps");

            migrationBuilder.RenameColumn(
                name: "StepName",
                table: "ApprovalSteps",
                newName: "Role");
        }
    }
}
