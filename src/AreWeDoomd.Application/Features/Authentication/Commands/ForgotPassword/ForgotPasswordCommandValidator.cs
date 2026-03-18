using FluentValidation;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();
    }
}
