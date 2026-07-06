using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AreWeDoomd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScoringModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThinkingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ScoringTokensPerAccount = table.Column<int>(type: "int", nullable: false),
                    CompositionTokensPerPost = table.Column<int>(type: "int", nullable: false),
                    PersonaTokensPerPersona = table.Column<int>(type: "int", nullable: false),
                    ReplyMaxTokens = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmSettings");
        }
    }
}
