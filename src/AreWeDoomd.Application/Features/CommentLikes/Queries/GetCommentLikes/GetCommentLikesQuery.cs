using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.CommentLikes.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.CommentLikes.Queries.GetCommentLikes;

public sealed record GetCommentLikesQuery(
    Guid PostId,
    Guid CommentId) : IRequest<Result<IReadOnlyList<CommentLikeUserResult>>>;
