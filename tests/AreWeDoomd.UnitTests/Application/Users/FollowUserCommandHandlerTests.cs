using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Commands.FollowUser;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Users;

public sealed class FollowUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserFollowRepository> _follows = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private FollowUserCommandHandler CreateHandler()
        => new(_users.Object, _follows.Object, _clock.Object, _uow.Object);

    private static User Target(string username)
        => User.Create(username, $"{username}@b.com", "valid-hash-string-1234", UserType.Human, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_WhenAlreadyFollowing_ShouldBeIdempotentSuccess()
    {
        var target = Target("driftwood");
        var requester = Guid.NewGuid();
        _users.Setup(r => r.GetByUsernameAsync("driftwood", It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _follows.Setup(f => f.ExistsAsync(requester, target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _follows.Setup(f => f.CountFollowersAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var result = await CreateHandler().Handle(new FollowUserCommand(requester, "driftwood"), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsFollowedByMe.ShouldBeTrue();
        result.Value.FollowerCount.ShouldBe(7);
        _follows.Verify(f => f.AddAsync(It.IsAny<UserFollow>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotFollowing_ShouldAddAndReturnState()
    {
        var target = Target("synthwave");
        var requester = Guid.NewGuid();
        _clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        _users.Setup(r => r.GetByUsernameAsync("synthwave", It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _follows.Setup(f => f.ExistsAsync(requester, target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _follows.Setup(f => f.CountFollowersAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(new FollowUserCommand(requester, "synthwave"), default);

        result.Value!.IsFollowedByMe.ShouldBeTrue();
        result.Value.FollowerCount.ShouldBe(1);
        _follows.Verify(f => f.AddAsync(It.IsAny<UserFollow>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTargetMissing_ShouldReturnNotFound()
    {
        _users.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var result = await CreateHandler().Handle(new FollowUserCommand(Guid.NewGuid(), "ghost"), default);
        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenFollowingSelf_ShouldReturnNotFoundOrFailure()
    {
        var self = Target("me_user");
        _users.Setup(r => r.GetByUsernameAsync("me_user", It.IsAny<CancellationToken>())).ReturnsAsync(self);
        var result = await CreateHandler().Handle(new FollowUserCommand(self.Id, "me_user"), default);
        result.IsSuccess.ShouldBeFalse();
    }
}
