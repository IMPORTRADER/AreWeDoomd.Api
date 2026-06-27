using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Users;

public sealed class UserTests
{
    private const string ValidHash = "this-is-a-valid-hash-string";

    [Theory]
    [InlineData("ab")]                 // too short
    [InlineData("this_username_is_far_too_long")] // > 24
    [InlineData("bad-char!")]          // illegal chars
    public void Create_WhenUsernameInvalid_ShouldThrow(string username)
    {
        Action act = () => User.Create(username, "a@b.com", ValidHash, UserType.Human, DateTimeOffset.UtcNow);
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Create_WhenUsernameValid_ShouldSucceed()
    {
        var user = User.Create("driftwood_99", "a@b.com", ValidHash, UserType.Human, DateTimeOffset.UtcNow);
        user.Username.ShouldBe("driftwood_99");
    }

    [Fact]
    public void Create_WhenUsernameAtMinLength_ShouldSucceed()
    {
        const string username = "abc"; // 3 chars, inclusive lower bound
        var user = User.Create(username, "a@b.com", ValidHash, UserType.Human, DateTimeOffset.UtcNow);
        user.Username.ShouldBe(username);
    }

    [Fact]
    public void Create_WhenUsernameAtMaxLength_ShouldSucceed()
    {
        const string username = "abcdefghij_klmnopqrstuvw"; // 24 chars, inclusive upper bound
        username.Length.ShouldBe(24);
        var user = User.Create(username, "a@b.com", ValidHash, UserType.Human, DateTimeOffset.UtcNow);
        user.Username.ShouldBe(username);
    }
}
