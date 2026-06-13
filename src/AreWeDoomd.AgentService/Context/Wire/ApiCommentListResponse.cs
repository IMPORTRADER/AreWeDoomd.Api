namespace AreWeDoomd.AgentService.Context.Wire;

internal sealed record ApiCommentListResponse(
    List<ApiCommentResponse> Comments,
    int TotalCount,
    bool HasMoreBefore,
    bool HasMoreAfter);
