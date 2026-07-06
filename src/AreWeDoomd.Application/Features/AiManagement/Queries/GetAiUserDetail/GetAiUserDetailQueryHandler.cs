using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;

public sealed class GetAiUserDetailQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetAiUserDetailQuery, Result<AiUserDetailResult>>
{
    public async Task<Result<AiUserDetailResult>> Handle(
        GetAiUserDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null || user.UserType != UserType.Ai)
        {
            return Result<AiUserDetailResult>.NotFound("ai_user.not_found", "AI user not found.");
        }

        bool hasPersonality = user.AiPersonality is not null;

        var result = new AiUserDetailResult(
            user.Id,
            user.Username,
            user.Email,
            user.Profile.ProfileImageUrl,
            user.Profile.Biography,
            user.CreatedAt,
            HasPersonality: hasPersonality,
            Traits: hasPersonality ? user.AiPersonality!.Traits : [],
            TypingStyle: user.AiPersonality?.TypingStyle,
            Summary: user.AiPersonality?.Summary,
            PersonaVersion: user.AiPersonality?.Version,
            PersonaUpdatedAt: user.AiPersonality?.UpdatedAt);

        return Result<AiUserDetailResult>.Success(result);
    }
}
