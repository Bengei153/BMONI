using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credit_assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LoanId = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    WalletSignalsScore = table.Column<int>(nullable: false),
                    PurposeClarity = table.Column<int>(nullable: false),
                    AmountRealism = table.Column<int>(nullable: false),
                    RepaymentPlanFit = table.Column<int>(nullable: false),
                    FinalScore = table.Column<int>(nullable: false),
                    AiAvailable = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_assessments_loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_assessments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_assessments_LoanId",
                table: "credit_assessments",
                column: "LoanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_assessments_UserId_CreatedAt",
                table: "credit_assessments",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_assessments");
        }
    }
}
