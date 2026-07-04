using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AreWeDoomd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveBulkJobTrackingToDedicatedTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedByBulkJobId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedByBulkJobId",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "BulkCreationRecords",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkCreationRecords", x => new { x.JobId, x.UserId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulkCreationRecords_JobId",
                table: "BulkCreationRecords",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkCreationRecords");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByBulkJobId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedByBulkJobId",
                table: "Users",
                column: "CreatedByBulkJobId",
                filter: "[CreatedByBulkJobId] IS NOT NULL");
        }
    }
}
