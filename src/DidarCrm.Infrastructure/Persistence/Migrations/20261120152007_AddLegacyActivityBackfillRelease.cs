using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DidarCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyActivityBackfillRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crmActivityBackfillRelease",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crmActivityBackfillRelease", x => x.ActivityId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crmActivityBackfillRelease_MessageId",
                table: "crmActivityBackfillRelease",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crmActivityBackfillRelease");
        }
    }
}
