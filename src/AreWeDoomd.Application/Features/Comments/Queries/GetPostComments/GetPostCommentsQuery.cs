using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;

public sealed record GetPostCommentsQuery(Guid PostId) : IRequest<Result<IReadOnlyList<CommentResult>>>;
