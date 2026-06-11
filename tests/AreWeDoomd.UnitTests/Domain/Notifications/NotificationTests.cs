using AreWeDoomd.Domain.Notifications;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Notifications;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    private static Notification CreateValid()
    {
        return Notification.Create(
            userId: Guid.NewGuid(),
            activityId: "act_1",
            activityType: "CommentCreated",
            actorName: "MiraStone",
            actorType: "Human",
            template: "post.comment.created",
            paramsJson: "{}",
            dedupeKey: "post:1:comment:1",
            createdAt: CreatedAt);
    }

    [Fact]
    public void Create_WhenArgumentsValid_ShouldInitializeUnreadNotification()
    {
        var notification = CreateValid();

        notification.Id.ShouldNotBe(Guid.Empty);
        notification.IsRead.ShouldBeFalse();
        notification.ReadAt.ShouldBeNull();
        notification.CreatedAt.ShouldBe(CreatedAt);
    }

    [Fact]
    public void Create_WhenUserIdEmpty_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Notification.Create(
            Guid.Empty, "act_1", "CommentCreated", "MiraStone", "Human",
            "post.comment.created", "{}", "dedupe", CreatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenDedupeKeyBlank_ShouldThrow(string dedupeKey)
    {
        Should.Throw<ArgumentException>(() => Notification.Create(
            Guid.NewGuid(), "act_1", "CommentCreated", "MiraStone", "Human",
            "post.comment.created", "{}", dedupeKey, CreatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenActivityTypeBlank_ShouldThrow(string activityType)
    {
        Should.Throw<ArgumentException>(() => Notification.Create(
            Guid.NewGuid(), "act_1", activityType, "MiraStone", "Human",
            "post.comment.created", "{}", "dedupe", CreatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenActorNameBlank_ShouldThrow(string actorName)
    {
        Should.Throw<ArgumentException>(() => Notification.Create(
            Guid.NewGuid(), "act_1", "CommentCreated", actorName, "Human",
            "post.comment.created", "{}", "dedupe", CreatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenActorTypeBlank_ShouldThrow(string actorType)
    {
        Should.Throw<ArgumentException>(() => Notification.Create(
            Guid.NewGuid(), "act_1", "CommentCreated", "MiraStone", actorType,
            "post.comment.created", "{}", "dedupe", CreatedAt));
    }

    [Fact]
    public void MarkAsRead_WhenUnread_ShouldSetReadStateAndTimestamp()
    {
        var notification = CreateValid();
        var readAt = CreatedAt.AddMinutes(5);

        notification.MarkAsRead(readAt);

        notification.IsRead.ShouldBeTrue();
        notification.ReadAt.ShouldBe(readAt);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_ShouldKeepOriginalTimestamp()
    {
        var notification = CreateValid();
        var firstReadAt = CreatedAt.AddMinutes(5);
        notification.MarkAsRead(firstReadAt);

        notification.MarkAsRead(CreatedAt.AddMinutes(10));

        notification.ReadAt.ShouldBe(firstReadAt);
    }
}
