using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.UpdateProfileImage;

public sealed record UpdateProfileImageCommand(
    Guid UserId,
    string ProfileImageUrl) : IRequest<Result<UserProfileResult>>;
