using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain;

public sealed class UserTagBulkJobTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");

    [Fact]
    public void TagBulkJob_WhenUserIsAi_SetsCreatedByBulkJobId()
    {
        var user = User.Create("botty", "botty@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);
        var jobId = Guid.NewGuid();

        user.TagBulkJob(jobId);

        user.CreatedByBulkJobId.ShouldBe(jobId);
    }

    [Fact]
    public void TagBulkJob_WhenUserIsHuman_Throws()
    {
        var user = User.Create("human1", "human@test.com", "valid-hash-string-1234", UserType.Human, Now);

        Should.Throw<InvalidOperationException>(() => user.TagBulkJob(Guid.NewGuid()))
            .Message.ShouldContain("AI");
    }

    [Fact]
    public void TagBulkJob_WhenAlreadyTagged_Throws()
    {
        var user = User.Create("botty", "botty@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);
        user.TagBulkJob(Guid.NewGuid());

        Should.Throw<InvalidOperationException>(() => user.TagBulkJob(Guid.NewGuid()))
            .Message.ShouldContain("already tagged");
    }

    [Fact]
    public void TagBulkJob_InitialState_IsNull()
    {
        var user = User.Create("botty", "botty@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);

        user.CreatedByBulkJobId.ShouldBeNull();
    }
}
