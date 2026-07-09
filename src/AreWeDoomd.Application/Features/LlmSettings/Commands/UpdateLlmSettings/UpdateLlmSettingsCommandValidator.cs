using FluentValidation;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;

public sealed class UpdateLlmSettingsCommandValidator : AbstractValidator<UpdateLlmSettingsCommand>
{
    public UpdateLlmSettingsCommandValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScoringModel).MaximumLength(200);
        RuleFor(x => x.ScoringTokensPerAccount)
            .InclusiveBetween(DomainLlmSettings.MinTokenBudget, DomainLlmSettings.MaxTokenBudget);
        RuleFor(x => x.CompositionTokensPerPost)
            .InclusiveBetween(DomainLlmSettings.MinTokenBudget, DomainLlmSettings.MaxTokenBudget);
        RuleFor(x => x.ReplyMaxTokens)
            .InclusiveBetween(DomainLlmSettings.MinTokenBudget, DomainLlmSettings.MaxTokenBudget);
    }
}
