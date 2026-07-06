using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class JwtAccessTokenGeneratorTests
{
    private const string ValidHash = "this-is-a-valid-hash-string";
    private const string TestKey = "test-signing-key-at-least-32-chars!!";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static JwtAccessTokenGenerator CreateGenerator()
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = TestKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenExpirationMinutes = 60
        });

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(Now);

        return new JwtAccessTokenGenerator(jwtOptions, dateTimeProvider.Object);
    }

    [Fact]
    public void Generate_WhenUserIsAdmin_ShouldIncludeIsAdminClaim()
    {
        var generator = CreateGenerator();
        var user = User.Create("adminuser", "admin@example.com", ValidHash, UserType.Human, Now);
        user.GrantAdmin(Now);

        var token = generator.Generate(user);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var isAdminClaim = parsed.Claims.FirstOrDefault(c => c.Type == "is_admin");
        isAdminClaim.ShouldNotBeNull();
        isAdminClaim.Value.ShouldBe("true");
    }

    [Fact]
    public void Generate_WhenUserIsNotAdmin_ShouldNotIncludeIsAdminClaim()
    {
        var generator = CreateGenerator();
        var user = User.Create("normaluser", "user@example.com", ValidHash, UserType.Human, Now);

        var token = generator.Generate(user);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var isAdminClaim = parsed.Claims.FirstOrDefault(c => c.Type == "is_admin");
        isAdminClaim.ShouldBeNull();
    }

    [Fact]
    public void Generate_ShouldAlwaysIncludeRoleClaim()
    {
        var generator = CreateGenerator();
        var user = User.Create("normaluser", "user@example.com", ValidHash, UserType.Human, Now);

        var token = generator.Generate(user);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        parsed.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Human");
    }
}
