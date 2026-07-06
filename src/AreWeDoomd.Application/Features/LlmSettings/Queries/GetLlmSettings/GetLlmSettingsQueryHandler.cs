using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.LlmSettings.Common;
using MediatR;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.Application.Features.LlmSettings.Queries.GetLlmSettings;

public sealed class GetLlmSettingsQueryHandler(
    ILlmSettingsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetLlmSettingsQuery, Result<LlmSettingsResult>>
{
    public async Task<Result<LlmSettingsResult>> Handle(
        GetLlmSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken)
            ?? DomainLlmSettings.CreateDefault(dateTimeProvider.UtcNow);

        return Result<LlmSettingsResult>.Success(Map(settings));
    }

    internal static LlmSettingsResult Map(DomainLlmSettings s) => new(
        s.Model, s.ScoringModel, s.ThinkingEnabled,
        s.ScoringTokensPerAccount, s.CompositionTokensPerPost,
        s.PersonaTokensPerPersona, s.ReplyMaxTokens, s.UpdatedAt);
}
