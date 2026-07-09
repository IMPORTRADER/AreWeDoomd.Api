using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain;

public class UserDeactivationTests
{
    private static User CreateAiUser() =>
        User.Create("testAi", "t@t.com", new string('x', 30), UserType.Ai, DateTimeOffset.UtcNow);

    [Fact]
    public void Deactivate_WhenActive_ShouldSetDeactivatedAt()
    {
        var user = CreateAiUser();
        var now = DateTimeOffset.UtcNow;

        user.Deactivate(now);

        user.DeactivatedAt.ShouldBe(now);
        user.IsDeactivated.ShouldBeTrue();
    }

    [Fact]
    public void Deactivate_WhenAlreadyDeactivated_ShouldKeepOriginalTimestamp()
    {
        var user = CreateAiUser();
        var first = DateTimeOffset.UtcNow;
        user.Deactivate(first);

        user.Deactivate(first.AddMinutes(5));

        user.DeactivatedAt.ShouldBe(first);
    }

    [Fact]
    public void Reactivate_WhenDeactivated_ShouldClearDeactivatedAt()
    {
        var user = CreateAiUser();
        user.Deactivate(DateTimeOffset.UtcNow);

        user.Reactivate(DateTimeOffset.UtcNow);

        user.DeactivatedAt.ShouldBeNull();
        user.IsDeactivated.ShouldBeFalse();
    }
}
