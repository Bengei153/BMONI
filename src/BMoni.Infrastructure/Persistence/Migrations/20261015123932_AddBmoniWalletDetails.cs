using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBmoniWalletDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BmoniOnboardingStatus",
                table: "users",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BmoniOwnerAddress",
                table: "users",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BmoniSmartWalletId",
                table: "users",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BmoniWalletAddress",
                table: "users",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BmoniOnboardingStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BmoniOwnerAddress",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BmoniSmartWalletId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BmoniWalletAddress",
                table: "users");
        }
    }
}
