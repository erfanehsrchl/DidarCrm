using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DidarCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crmNotification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crmNotification", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_crmNotification_ActivityId",
                table: "crmNotification",
                column: "ActivityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crmNotification");
        }
    }
}
