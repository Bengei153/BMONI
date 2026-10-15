using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMoni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBmoniWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bmoni_webhook_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    EventId = table.Column<string>(maxLength: 200, nullable: false),
                    EventType = table.Column<string>(maxLength: 160, nullable: false),
                    BmoniUserId = table.Column<string>(maxLength: 128, nullable: true),
                    Status = table.Column<string>(maxLength: 80, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bmoni_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bmoni_webhook_events_EventId",
                table: "bmoni_webhook_events",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bmoni_webhook_events");
        }
    }
}
