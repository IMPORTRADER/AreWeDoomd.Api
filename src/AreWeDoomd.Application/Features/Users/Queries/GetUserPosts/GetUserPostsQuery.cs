using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserPosts;

public sealed record GetUserPostsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<UserPostResult>>>;
