using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "loans",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "loan_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LoanId = table.Column<Guid>(nullable: false),
                    Number = table.Column<int>(nullable: false),
                    DueAt = table.Column<DateTimeOffset>(nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_installments_loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_installments_LoanId_Number",
                table: "loan_installments",
                columns: new[] { "LoanId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_installments");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "loans");
        }
    }
}
