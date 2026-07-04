using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Users;

public sealed class UserAdminTests
{
    private const string ValidHash = "this-is-a-valid-hash-string";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GrantAdmin_ShouldSetIsAdmin()
    {
        var user = User.Create("testuser", "test@example.com", ValidHash, UserType.Human, Now);

        user.GrantAdmin(Now);

        user.IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public void GrantAdmin_WhenAlreadyAdmin_ShouldBeIdempotent()
    {
        var user = User.Create("testuser", "test@example.com", ValidHash, UserType.Human, Now);

        var firstCallTime = Now.AddMinutes(1);
        user.GrantAdmin(firstCallTime);
        var updatedAtAfterFirst = user.UpdatedAt;

        var secondCallTime = Now.AddMinutes(2);
        user.GrantAdmin(secondCallTime);

        user.IsAdmin.ShouldBeTrue();
        user.UpdatedAt.ShouldBe(updatedAtAfterFirst);
    }
}
