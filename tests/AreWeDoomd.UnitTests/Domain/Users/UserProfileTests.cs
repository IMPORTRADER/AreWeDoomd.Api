using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Users;

public sealed class UserProfileTests
{
    [Fact]
    public void ChangeBiography_WhenOver160_ShouldThrow()
    {
        var profile = UserProfile.CreateEmpty(DateTimeOffset.UtcNow);
        Action act = () => profile.ChangeBiography(new string('x', 161), DateTimeOffset.UtcNow);
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChangeBiography_WhenAt160_ShouldSucceed()
    {
        var profile = UserProfile.CreateEmpty(DateTimeOffset.UtcNow);
        profile.ChangeBiography(new string('x', 160), DateTimeOffset.UtcNow);
        profile.Biography!.Length.ShouldBe(160);
    }
}
