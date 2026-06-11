using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Notifications.Queries.GetUnreadCount;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Notifications;

public sealed class GetUnreadNotificationCountQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _repository = new();

    [Fact]
    public async Task Handle_WhenUnreadNotificationsExist_ShouldReturnCount()
    {
        var userId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = new GetUnreadNotificationCountQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetUnreadNotificationCountQuery(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
    }

    [Fact]
    public async Task Handle_WhenNoUnreadNotifications_ShouldReturnZero()
    {
        var userId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetUnreadNotificationCountQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetUnreadNotificationCountQuery(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
    }
}
