using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetUserProfile;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Users;

public sealed class GetUserProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserFollowRepository> _follows = new();
    private readonly Mock<IProfileStatsRepository> _stats = new();

    private GetUserProfileQueryHandler CreateHandler() =>
        new(_users.Object, _follows.Object, _stats.Object);

    private static User MakeUser(string username) =>
        User.Create(username, $"{username}@b.com", "valid-hash-string-1234", UserType.Human, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_WhenUserMissing_ShouldReturnNotFound()
    {
        _users.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new GetUserProfileQuery("ghost", null), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(AreWeDoomd.Application.Common.Results.ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenRequesterIsSameUser_ShouldSetIsMeTrueAndIsFollowedFalse()
    {
        var user = MakeUser("driftwood");
        _users.Setup(r => r.GetByUsernameAsync("driftwood", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _stats.Setup(r => r.GetStatsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileStatsResult(1, 2, 3, 4, 5));

        var result = await CreateHandler().Handle(new GetUserProfileQuery("driftwood", user.Id), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsMe.ShouldBeTrue();
        result.Value.IsFollowedByMe.ShouldBeFalse();
        result.Value.Badges.Count.ShouldBe(1);
        result.Value.Stats.FollowerCount.ShouldBe(4);
        _follows.Verify(f => f.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRequesterFollows_ShouldSetIsFollowedByMeTrue()
    {
        var user = MakeUser("synthwave");
        var requester = Guid.NewGuid();
        _users.Setup(r => r.GetByUsernameAsync("synthwave", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _stats.Setup(r => r.GetStatsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileStatsResult(0, 0, 0, 0, 0));
        _follows.Setup(f => f.ExistsAsync(requester, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateHandler().Handle(new GetUserProfileQuery("synthwave", requester), default);

        result.Value!.IsFollowedByMe.ShouldBeTrue();
        result.Value.IsMe.ShouldBeFalse();
    }
}
