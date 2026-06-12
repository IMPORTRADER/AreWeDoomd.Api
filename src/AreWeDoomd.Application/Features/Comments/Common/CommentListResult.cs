namespace AreWeDoomd.Application.Features.Comments.Common;

public sealed record CommentListResult(
    IReadOnlyList<CommentResult> Comments,
    int TotalCount,
    bool HasMoreBefore,
    bool HasMoreAfter);
