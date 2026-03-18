using FluentValidation;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
