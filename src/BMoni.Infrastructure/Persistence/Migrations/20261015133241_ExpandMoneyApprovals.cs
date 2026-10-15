using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMoneyApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "LoanId",
                table: "approvals",
                nullable: true,
                oldClrType: typeof(Guid));

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationIdempotencyKey",
                table: "approvals",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "approvals",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Rejected",
                table: "approvals",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_approvals_ConfirmationIdempotencyKey",
                table: "approvals",
                column: "ConfirmationIdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_approvals_ConfirmationIdempotencyKey",
                table: "approvals");

            migrationBuilder.DropColumn(
                name: "ConfirmationIdempotencyKey",
                table: "approvals");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "approvals");

            migrationBuilder.DropColumn(
                name: "Rejected",
                table: "approvals");

            migrationBuilder.AlterColumn<Guid>(
                name: "LoanId",
                table: "approvals",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldNullable: true);
        }
    }
}
