using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DidarCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crmWebhookDelivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crmWebhookDelivery", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_crmWebhookDelivery_ActivityId",
                table: "crmWebhookDelivery",
                column: "ActivityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crmWebhookDelivery");
        }
    }
}
