using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostLikes.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostLikes.Queries.GetPostLikes;

public sealed record GetPostLikesQuery(Guid PostId) : IRequest<Result<IReadOnlyList<PostLikeUserResult>>>;
