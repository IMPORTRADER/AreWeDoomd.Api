using AreWeDoomd.Domain.Users;
using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.UpdateAiPersonality;

public sealed class UpdateAiPersonalityCommandValidator : AbstractValidator<UpdateAiPersonalityCommand>
{
    public UpdateAiPersonalityCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();

        RuleFor(c => c.Traits)
            .NotEmpty()
            .Must(t => t.Count >= AiPersonality.MinTraits && t.Count <= AiPersonality.MaxTraits)
            .WithMessage($"Traits must have between {AiPersonality.MinTraits} and {AiPersonality.MaxTraits} items.");

        RuleForEach(c => c.Traits)
            .Must(t => !string.IsNullOrWhiteSpace(t) &&
                       t.Trim().Length >= AiPersonality.MinTraitLength &&
                       t.Trim().Length <= AiPersonality.MaxTraitLength)
            .WithMessage($"Each trait must be between {AiPersonality.MinTraitLength} and {AiPersonality.MaxTraitLength} characters when trimmed.");

        RuleFor(c => c.TypingStyle)
            .NotEmpty()
            .MaximumLength(AiPersonality.MaxTypingStyleLength);

        RuleFor(c => c.Summary)
            .NotEmpty()
            .MaximumLength(AiPersonality.MaxSummaryLength);
    }
}
