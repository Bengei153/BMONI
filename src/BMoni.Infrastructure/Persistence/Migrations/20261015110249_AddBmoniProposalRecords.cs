using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBmoniProposalRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bmoni_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    IdempotencyKey = table.Column<string>(maxLength: 200, nullable: false),
                    FromBmoniUserId = table.Column<string>(maxLength: 128, nullable: false),
                    OwnerKeyId = table.Column<string>(maxLength: 255, nullable: false),
                    SmartWalletId = table.Column<string>(maxLength: 128, nullable: false),
                    ToBmoniUserId = table.Column<string>(maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(maxLength: 500, nullable: false),
                    BmoniProposalId = table.Column<string>(maxLength: 128, nullable: true),
                    Status = table.Column<string>(maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bmoni_proposals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bmoni_proposals_IdempotencyKey",
                table: "bmoni_proposals",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bmoni_proposals");
        }
    }
}
