using AreWeDoomd.Application.Common.Interfaces;
using FluentValidation;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;

public sealed class UpdateLlmSettingsCommandValidator : AbstractValidator<UpdateLlmSettingsCommand>
{
    public UpdateLlmSettingsCommandValidator(IChatProviderCatalog chatProviderCatalog)
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScoringModel).MaximumLength(200);
        RuleFor(x => x.Provider)
            .MaximumLength(50)
            .Must(p => string.IsNullOrWhiteSpace(p) || chatProviderCatalog.Exists(p.Trim()))
            .WithMessage("Provider is not a registered chat provider.");
        RuleFor(x => x.ScoringTokensPerAccount)
            .InclusiveBetween(DomainLlmSettings.MinTokenBudget, DomainLlmSettings.MaxTokenBudget);
        RuleFor(x => x.CompositionTokensPerPost)
            .InclusiveBetween(DomainLlmSettings.MinTokenBudget, DomainLlmSettings.MaxTokenBudget);
        RuleFor(x => x.ReplyMaxTokens)
            .InclusiveBetween(DomainLlmSettings.MinTokenBudget, DomainLlmSettings.MaxTokenBudget);
    }
}
