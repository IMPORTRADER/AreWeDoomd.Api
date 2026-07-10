using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.BulkDeactivateAiUsers;

public sealed class BulkDeactivateAiUsersCommandValidator : AbstractValidator<BulkDeactivateAiUsersCommand>
{
    public BulkDeactivateAiUsersCommandValidator()
    {
        RuleFor(x => x.UserIds).NotEmpty();
        RuleFor(x => x.UserIds.Count).LessThanOrEqualTo(200)
            .WithMessage("At most 200 users per request.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}
