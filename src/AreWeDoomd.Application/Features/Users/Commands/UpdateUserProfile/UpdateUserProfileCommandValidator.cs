using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        When(x => x.Username is not null, () =>
        {
            RuleFor(x => x.Username!)
                .MinimumLength(3)
                .MaximumLength(24)
                .Matches("^[a-zA-Z0-9_]+$")
                .WithMessage("Username may only contain letters, digits and underscore.");
        });

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email!)
                .MaximumLength(254)
                .Must(ContainAtSign)
                .WithMessage("Email is invalid.");
        });

        When(x => x.Biography is not null, () =>
        {
            RuleFor(x => x.Biography!)
                .MaximumLength(160);
        });
    }

    private static bool ContainAtSign(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && email.Trim().Contains('@');
    }
}
