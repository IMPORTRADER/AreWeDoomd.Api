using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AreWeDoomd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeactivatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAt",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Users");
        }
    }
}
