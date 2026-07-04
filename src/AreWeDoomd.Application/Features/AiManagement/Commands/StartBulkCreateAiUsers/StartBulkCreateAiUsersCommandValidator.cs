using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.StartBulkCreateAiUsers;

public sealed class StartBulkCreateAiUsersCommandValidator : AbstractValidator<StartBulkCreateAiUsersCommand>
{
    public StartBulkCreateAiUsersCommandValidator()
    {
        RuleFor(x => x.Count)
            .InclusiveBetween(1, 50)
            .WithMessage("Count must be between 1 and 50.");
    }
}
