using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.LlmSettings.Common;
using AreWeDoomd.Application.Features.LlmSettings.Queries.GetLlmSettings;
using MediatR;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;

public sealed class UpdateLlmSettingsCommandHandler(
    ILlmSettingsRepository repository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateLlmSettingsCommand, Result<LlmSettingsResult>>
{
    public async Task<Result<LlmSettingsResult>> Handle(
        UpdateLlmSettingsCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var settings = await repository.GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = DomainLlmSettings.CreateDefault(now);
            await repository.AddAsync(settings, cancellationToken);
        }

        settings.Update(
            request.Model, request.ScoringModel, request.ThinkingEnabled,
            provider: string.Empty,
            request.ScoringTokensPerAccount, request.CompositionTokensPerPost,
            request.ReplyMaxTokens, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LlmSettingsResult>.Success(GetLlmSettingsQueryHandler.Map(settings));
    }
}
