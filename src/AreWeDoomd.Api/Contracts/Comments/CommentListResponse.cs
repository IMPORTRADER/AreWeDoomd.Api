namespace AreWeDoomd.Api.Contracts.Comments;

public sealed record CommentListResponse(
    List<CommentResponse> Comments,
    int TotalCount,
    bool HasMoreBefore,
    bool HasMoreAfter);
