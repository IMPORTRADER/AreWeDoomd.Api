using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.CancelScheduledPost;

public sealed record CancelScheduledPostCommand(Guid ScheduledPostId) : IRequest<Result<Unit>>;
