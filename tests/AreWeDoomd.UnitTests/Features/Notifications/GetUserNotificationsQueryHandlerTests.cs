using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Notifications.Queries.GetUserNotifications;
using AreWeDoomd.Domain.Notifications;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Notifications;

public sealed class GetUserNotificationsQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _repository = new();

    [Fact]
    public async Task Handle_WhenNotificationsExist_ShouldReturnMappedDtosNewestFirst()
    {
        var userId = Guid.NewGuid();
        var entity = Notification.Create(
            userId, "act_1", "CommentCreated", "MiraStone", "Human",
            "post.comment.created",
            "{\"actor_name\":\"MiraStone\",\"comment_preview\":\"hi\"}",
            "dedupe-1", DateTimeOffset.UtcNow);

        _repository
            .Setup(r => r.GetRecentAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entity]);

        var handler = new GetUserNotificationsQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetUserNotificationsQuery(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value[0].Template.ShouldBe("post.comment.created");
        result.Value[0].Params["actor_name"].ShouldBe("MiraStone");
        result.Value[0].ActorName.ShouldBe("MiraStone");
    }

    [Fact]
    public async Task Handle_WhenNoNotifications_ShouldReturnEmptyList()
    {
        var userId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetRecentAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetUserNotificationsQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetUserNotificationsQuery(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ShouldBeEmpty();
    }
}
