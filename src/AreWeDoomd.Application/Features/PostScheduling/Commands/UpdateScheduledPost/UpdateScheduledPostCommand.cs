using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.UpdateScheduledPost;

public sealed record UpdateScheduledPostCommand(
    Guid ScheduledPostId,
    string Content,
    DateTimeOffset ScheduledAtUtc) : IRequest<Result<Unit>>;
