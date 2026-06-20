using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AreWeDoomd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostCommentLikeCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommentLikeCount",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE p
                SET p.CommentLikeCount = ISNULL(c.Total, 0)
                FROM Posts p
                LEFT JOIN (
                    SELECT PostId, SUM(LikeCount) AS Total
                    FROM Comments
                    GROUP BY PostId
                ) c ON c.PostId = p.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentLikeCount",
                table: "Posts");
        }
    }
}
