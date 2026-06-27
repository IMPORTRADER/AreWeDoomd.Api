using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserProfile;

public sealed record GetUserProfileQuery(
    string Username,
    Guid? RequesterId) : IRequest<Result<UserProfileDetailResult>>;
