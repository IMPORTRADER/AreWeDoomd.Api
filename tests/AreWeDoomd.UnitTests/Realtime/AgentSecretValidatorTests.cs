using AreWeDoomd.Api.Realtime;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Realtime;

public sealed class AgentSecretValidatorTests
{
    [Fact]
    public void IsValid_ReturnsTrue_WhenSecretsMatch()
    {
        AgentSecretValidator.IsValid("super-secret", "super-secret").ShouldBeTrue();
    }

    [Theory]
    [InlineData("super-secret", "wrong-secret")]
    [InlineData("super-secret", "")]
    [InlineData("", "super-secret")]
    [InlineData(null, "super-secret")]
    [InlineData("super-secret", null)]
    [InlineData("short", "a-much-longer-secret-value")]
    public void IsValid_ReturnsFalse_WhenSecretsDoNotMatchOrMissing(string? provided, string? expected)
    {
        AgentSecretValidator.IsValid(provided, expected).ShouldBeFalse();
    }
}
