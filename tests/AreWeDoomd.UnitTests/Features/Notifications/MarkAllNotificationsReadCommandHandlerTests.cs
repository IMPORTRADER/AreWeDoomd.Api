using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Notifications.Commands.MarkAllRead;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Notifications;

public sealed class MarkAllNotificationsReadCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    [Fact]
    public async Task Handle_WhenUnreadNotificationsExist_ShouldMarkAllReadAndReturnCount()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.Setup(d => d.UtcNow).Returns(now);
        _repository
            .Setup(r => r.MarkAllReadAsync(userId, now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = new MarkAllNotificationsReadCommandHandler(_repository.Object, _dateTimeProvider.Object);

        var result = await handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        _repository.Verify(r => r.MarkAllReadAsync(userId, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoUnreadNotifications_ShouldReturnZero()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Setup(d => d.UtcNow).Returns(now);
        _repository
            .Setup(r => r.MarkAllReadAsync(userId, now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new MarkAllNotificationsReadCommandHandler(_repository.Object, _dateTimeProvider.Object);

        var result = await handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
    }
}
