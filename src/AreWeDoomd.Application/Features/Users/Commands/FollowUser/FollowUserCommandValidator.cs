using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Commands.FollowUser;

public sealed class FollowUserCommandValidator : AbstractValidator<FollowUserCommand>
{
    public FollowUserCommandValidator()
    {
        RuleFor(x => x.FollowerId)
            .NotEmpty();

        RuleFor(x => x.TargetUsername)
            .NotEmpty();
    }
}
