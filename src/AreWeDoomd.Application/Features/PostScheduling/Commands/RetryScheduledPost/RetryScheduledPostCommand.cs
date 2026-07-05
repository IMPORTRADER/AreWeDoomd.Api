using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.RetryScheduledPost;

public sealed record RetryScheduledPostCommand(Guid ScheduledPostId) : IRequest<Result<Unit>>;
