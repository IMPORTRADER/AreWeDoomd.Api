using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;

public sealed record GetPostCommentsQuery(
    Guid PostId,
    bool IncludeAllComments,
    CommentSortDirection Sort = CommentSortDirection.Asc,
    int? Limit = null,
    Guid? Anchor = null,
    int Around = 2,
    Guid? Before = null,
    Guid? After = null) : IRequest<Result<CommentListResult>>;
