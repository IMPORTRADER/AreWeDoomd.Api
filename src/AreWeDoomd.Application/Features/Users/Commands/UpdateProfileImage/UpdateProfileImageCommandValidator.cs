using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Commands.UpdateProfileImage;

public sealed class UpdateProfileImageCommandValidator : AbstractValidator<UpdateProfileImageCommand>
{
    public UpdateProfileImageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.ProfileImageUrl)
            .NotEmpty()
            .MaximumLength(2048);
    }
}
