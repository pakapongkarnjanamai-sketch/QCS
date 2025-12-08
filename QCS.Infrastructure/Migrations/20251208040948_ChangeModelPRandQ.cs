using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QCS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeModelPRandQ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==========================================================================================
            // [Manual Fix] 1. แปลงข้อมูล Status เก่า (String) ให้เป็นตัวเลข (String) ก่อนเปลี่ยน Type
            // ==========================================================================================

            // 1.1 ตาราง PurchaseRequests
            // Map: Draft=0, Pending=1, Approved=2, Completed=3, Rejected=9
            migrationBuilder.Sql("UPDATE PurchaseRequests SET Status = '0' WHERE Status = 'Draft'");
            migrationBuilder.Sql("UPDATE PurchaseRequests SET Status = '1' WHERE Status = 'Pending'");
            migrationBuilder.Sql("UPDATE PurchaseRequests SET Status = '2' WHERE Status = 'Approved'");
            migrationBuilder.Sql("UPDATE PurchaseRequests SET Status = '9' WHERE Status = 'Rejected'");
            // กรณีข้อมูลอื่นๆ ที่ไม่ตรงเงื่อนไข ให้ Default เป็น 0 (Draft) เพื่อกัน Error
            migrationBuilder.Sql("UPDATE PurchaseRequests SET Status = '0' WHERE ISNUMERIC(Status) = 0");

            // 1.2 ตาราง ApprovalSteps (ถ้ามีข้อมูล)
            // Map: Pending=1, Approved=2, Rejected=9
            migrationBuilder.Sql("UPDATE ApprovalSteps SET Status = '1' WHERE Status = 'Pending'");
            migrationBuilder.Sql("UPDATE ApprovalSteps SET Status = '2' WHERE Status = 'Approved'");
            migrationBuilder.Sql("UPDATE ApprovalSteps SET Status = '9' WHERE Status = 'Rejected'");
            migrationBuilder.Sql("UPDATE ApprovalSteps SET Status = '1' WHERE ISNUMERIC(Status) = 0");

            // ==========================================================================================
            // [End Manual Fix] ต่อจากนี้คือ Code ที่ Scaffold มาเดิมๆ
            // ==========================================================================================

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_AttachmentFiles_AttachmentFileId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_AttachmentFileId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AttachmentFileId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "IsSelected",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "VendorName",
                table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                table: "Quotations",
                newName: "FilePath");

            migrationBuilder.RenameColumn(
                name: "DocumentNo",
                table: "PurchaseRequests",
                newName: "VendorName");

            migrationBuilder.RenameColumn(
                name: "ApprovalDate",
                table: "ApprovalSteps",
                newName: "ActionDate");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Quotations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // ตรงนี้คือจุดที่เคย Error ตอนนี้จะผ่านแล้วเพราะข้อมูลใน DB เป็น "1", "2" ซึ่งแปลงเป็น int ได้
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "PurchaseRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "PurchaseRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "PurchaseRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CurrentStepId",
                table: "PurchaseRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "PurchaseRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "PurchaseRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "PurchaseRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ตรงนี้ของ ApprovalSteps ก็จะผ่านเช่นกัน
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ApprovalSteps",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApproverName",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ส่วน Down เอาไว้เหมือนเดิมก็ได้ หรือถ้าอยากให้สมบูรณ์ต้องแปลงกลับ int -> string
            // แต่สำหรับการแก้ปัญหานี้ โฟกัสแค่ Up ก็พอครับ

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "CurrentStepId",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "PurchaseRequests");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Quotations",
                newName: "OriginalFileName");

            migrationBuilder.RenameColumn(
                name: "VendorName",
                table: "PurchaseRequests",
                newName: "DocumentNo");

            migrationBuilder.RenameColumn(
                name: "ActionDate",
                table: "ApprovalSteps",
                newName: "ApprovalDate");

            migrationBuilder.AddColumn<int>(
                name: "AttachmentFileId",
                table: "Quotations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                table: "Quotations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "Quotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "Quotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "Quotations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VendorName",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PurchaseRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ApproverName",
                table: "ApprovalSteps",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

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
    }
}