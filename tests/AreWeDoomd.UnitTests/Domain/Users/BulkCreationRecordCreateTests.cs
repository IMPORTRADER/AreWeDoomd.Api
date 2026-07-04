using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Users;

public sealed class BulkCreationRecordCreateTests
{
    private static readonly Guid ValidJobId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const string ValidUsername = "botty";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T10:00:00Z");

    [Fact]
    public void Create_WithValidArgs_SetsAllProperties()
    {
        var record = BulkCreationRecord.Create(ValidJobId, ValidUserId, ValidUsername, Now);

        record.JobId.ShouldBe(ValidJobId);
        record.UserId.ShouldBe(ValidUserId);
        record.Username.ShouldBe(ValidUsername);
        record.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_WhenJobIdIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            BulkCreationRecord.Create(Guid.Empty, ValidUserId, ValidUsername, Now))
            .ParamName.ShouldBe("jobId");
    }

    [Fact]
    public void Create_WhenUserIdIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            BulkCreationRecord.Create(ValidJobId, Guid.Empty, ValidUsername, Now))
            .ParamName.ShouldBe("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenUsernameIsNullOrWhiteSpace_Throws(string? username)
    {
        Should.Throw<ArgumentException>(() =>
            BulkCreationRecord.Create(ValidJobId, ValidUserId, username!, Now))
            .ParamName.ShouldBe("username");
    }
}
