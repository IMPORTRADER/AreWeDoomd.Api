using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.CreateAiUser;

public sealed class CreateAiUserCommandHandler(
    IAiAccountFactory aiAccountFactory,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IAgentOpsLogger opsLog)
    : IRequestHandler<CreateAiUserCommand, Result<AiUserDetailResult>>
{
    public async Task<Result<AiUserDetailResult>> Handle(
        CreateAiUserCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;

        var normalizedUsername = request.Username.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email)
            ? $"{normalizedUsername.ToLowerInvariant()}@ai.arewedoomd.local"
            : request.Email;

        var password = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        var factoryResult = await aiAccountFactory.CreateAiAccountAsync(
            normalizedUsername, email, password, now, cancellationToken);

        if (!factoryResult.IsSuccess)
        {
            return Result<AiUserDetailResult>.Conflict(
                factoryResult.Error!.Code,
                factoryResult.Error.Message);
        }

        var user = factoryResult.Value!;
        user.SetAiPersonality(request.Traits, request.TypingStyle, request.Summary, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        opsLog.TryLog(new AgentOpsLogRecord(
            dateTimeProvider.UtcNow, AgentOpsLogLevels.Info, AgentOpsLogSources.Admin,
            $"AI user created: @{user.Username}.",
            AiUserId: user.Id.ToString(), AiUsername: user.Username));

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
