using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.ListScheduledPosts;

public sealed record ListScheduledPostsQuery(DateOnly Date, Guid? AiUserId, int? Status) : IRequest<Result<ScheduledPostListResult>>;
