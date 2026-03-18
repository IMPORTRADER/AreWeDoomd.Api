using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Commands.UnfollowUser;

public sealed class UnfollowUserCommandValidator : AbstractValidator<UnfollowUserCommand>
{
    public UnfollowUserCommandValidator()
    {
        RuleFor(x => x.FollowerId)
            .NotEmpty();

        RuleFor(x => x.FollowingId)
            .NotEmpty();
    }
}
