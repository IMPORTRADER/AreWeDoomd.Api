using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.UpdateAiPersonality;

public sealed record UpdateAiPersonalityCommand(
    Guid UserId,
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary)
    : IRequest<Result<AiUserDetailResult>>;
