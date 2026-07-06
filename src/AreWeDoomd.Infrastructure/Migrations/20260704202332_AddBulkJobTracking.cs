using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AreWeDoomd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkJobTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedByBulkJobId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedByBulkJobId",
                table: "Users");
        }
    }
}
