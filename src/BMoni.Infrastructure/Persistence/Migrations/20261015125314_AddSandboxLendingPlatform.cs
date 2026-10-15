using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxLendingPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(maxLength: 40, nullable: false),
                    ReferenceId = table.Column<Guid>(nullable: false),
                    IdempotencyKey = table.Column<string>(maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    BorrowerId = table.Column<Guid>(nullable: false),
                    Principal = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    FundedAmount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    RepaidAmount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Purpose = table.Column<string>(maxLength: 80, nullable: false),
                    Statement = table.Column<string>(maxLength: 1000, nullable: false),
                    RepaymentDays = table.Column<int>(nullable: false),
                    Installments = table.Column<int>(nullable: false),
                    CreditScore = table.Column<int>(nullable: false),
                    InterestRate = table.Column<decimal>(precision: 5, scale: 4, nullable: false),
                    TotalRepayable = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    FundingDeadline = table.Column<DateTimeOffset>(nullable: false),
                    Revision = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "simulated_wallets",
                columns: table => new
                {
                    UserId = table.Column<Guid>(nullable: false),
                    Balance = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Revision = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulated_wallets", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_simulated_wallets_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LoanId = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    Type = table.Column<string>(maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(maxLength: 200, nullable: false),
                    Confirmed = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approvals_loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LoanId = table.Column<Guid>(nullable: false),
                    LenderId = table.Column<Guid>(nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(maxLength: 200, nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_contributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_contributions_loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_repayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LoanId = table.Column<Guid>(nullable: false),
                    BorrowerId = table.Column<Guid>(nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_repayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_repayments_loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "repayment_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    RepaymentId = table.Column<Guid>(nullable: false),
                    LenderId = table.Column<Guid>(nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repayment_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repayment_shares_loan_repayments_RepaymentId",
                        column: x => x.RepaymentId,
                        principalTable: "loan_repayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approvals_IdempotencyKey",
                table: "approvals",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approvals_LoanId",
                table: "approvals",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_IdempotencyKey",
                table: "ledger_entries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_UserId_CreatedAt",
                table: "ledger_entries",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_contributions_IdempotencyKey",
                table: "loan_contributions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_contributions_LoanId_LenderId_Status",
                table: "loan_contributions",
                columns: new[] { "LoanId", "LenderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_repayments_IdempotencyKey",
                table: "loan_repayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_repayments_LoanId",
                table: "loan_repayments",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_loans_BorrowerId_Status",
                table: "loans",
                columns: new[] { "BorrowerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_repayment_shares_RepaymentId",
                table: "repayment_shares",
                column: "RepaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approvals");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "loan_contributions");

            migrationBuilder.DropTable(
                name: "repayment_shares");

            migrationBuilder.DropTable(
                name: "simulated_wallets");

            migrationBuilder.DropTable(
                name: "loan_repayments");

            migrationBuilder.DropTable(
                name: "loans");
        }
    }
}
