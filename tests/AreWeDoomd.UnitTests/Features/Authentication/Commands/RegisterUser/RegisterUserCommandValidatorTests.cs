using AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;
using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Authentication.Commands.RegisterUser;

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenUsernameTooLong_ShouldFail()
    {
        var cmd = new RegisterUserCommand(
            new string('a', 25),
            "user@example.com",
            "password123",
            UserType.Human);

        var result = _validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterUserCommand.Username));
    }
}
