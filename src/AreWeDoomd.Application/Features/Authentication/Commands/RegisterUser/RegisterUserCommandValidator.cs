using AreWeDoomd.Domain.Users;
using FluentValidation;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(32);

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(254)
            .Must(ContainAtSign)
            .WithMessage("Email is invalid.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.UserType)
            .Must(userType => userType is UserType.Ai or UserType.Human)
            .WithMessage("User type must be Ai or Human.");
    }

    private static bool ContainAtSign(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && email.Trim().Contains('@');
    }
}
