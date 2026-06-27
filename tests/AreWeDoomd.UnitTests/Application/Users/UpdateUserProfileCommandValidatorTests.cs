using System.Linq;
using AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Users;

public sealed class UpdateUserProfileCommandValidatorTests
{
    private readonly UpdateUserProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBioOver160_ShouldFail()
    {
        var cmd = new UpdateUserProfileCommand(Guid.NewGuid(), null, null, new string('x', 161));

        var result = _validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserProfileCommand.Biography));
    }

    [Fact]
    public void Validate_WhenUsernameHasIllegalChars_ShouldFail()
    {
        var cmd = new UpdateUserProfileCommand(Guid.NewGuid(), "bad-name!", null, null);

        var result = _validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserProfileCommand.Username));
    }

    [Fact]
    public void Validate_WhenUsernameTooLong_ShouldFail()
    {
        var cmd = new UpdateUserProfileCommand(Guid.NewGuid(), new string('a', 25), null, null);

        var result = _validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserProfileCommand.Username));
    }
}
