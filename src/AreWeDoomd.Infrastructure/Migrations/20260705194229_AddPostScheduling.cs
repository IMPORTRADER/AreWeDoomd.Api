using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AreWeDoomd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThresholdSnapshot = table.Column<int>(type: "int", nullable: false),
                    MaxPostsSnapshot = table.Column<int>(type: "int", nullable: false),
                    PostLengthGuideSnapshot = table.Column<int>(type: "int", nullable: false),
                    StrategySnapshot = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesireThreshold = table.Column<int>(type: "int", nullable: false),
                    MaxPostsPerDay = table.Column<int>(type: "int", nullable: false),
                    PostLengthGuide = table.Column<int>(type: "int", nullable: false),
                    LatePolicy = table.Column<int>(type: "int", nullable: false),
                    LateGraceHours = table.Column<int>(type: "int", nullable: false),
                    Strategy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DesireScore = table.Column<int>(type: "int", nullable: true),
                    Reasoning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedPostCount = table.Column<int>(type: "int", nullable: true),
                    DroppedPostCount = table.Column<int>(type: "int", nullable: false),
                    ModelUsed = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PushCount = table.Column<int>(type: "int", nullable: false),
                    LastPushedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRunItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleRunItems_ScheduleRuns_ScheduleRunId",
                        column: x => x.ScheduleRunId,
                        principalTable: "ScheduleRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleRunItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    WasTimeAdjusted = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledPosts_ScheduleRunItems_ScheduleRunItemId",
                        column: x => x.ScheduleRunItemId,
                        principalTable: "ScheduleRunItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledPosts_AiUserId",
                table: "ScheduledPosts",
                column: "AiUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledPosts_ScheduleRunItemId",
                table: "ScheduledPosts",
                column: "ScheduleRunItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledPosts_Status_ScheduledAtUtc",
                table: "ScheduledPosts",
                columns: new[] { "Status", "ScheduledAtUtc" },
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRunItems_AiUserId_RunDate",
                table: "ScheduleRunItems",
                columns: new[] { "AiUserId", "RunDate" },
                unique: true,
                filter: "[Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRunItems_ScheduleRunId",
                table: "ScheduleRunItems",
                column: "ScheduleRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRunItems_Status_LastPushedAtUtc",
                table: "ScheduleRunItems",
                columns: new[] { "Status", "LastPushedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRuns_RunDate",
                table: "ScheduleRuns",
                column: "RunDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledPosts");

            migrationBuilder.DropTable(
                name: "SchedulingSettings");

            migrationBuilder.DropTable(
                name: "ScheduleRunItems");

            migrationBuilder.DropTable(
                name: "ScheduleRuns");
        }
    }
}
