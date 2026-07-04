using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.CreateAiUser;

public sealed record CreateAiUserCommand(
    string Username,
    string? Email,
    IReadOnlyList<string> Traits,
    string TypingStyle,
    string Summary)
    : IRequest<Result<AiUserDetailResult>>;
