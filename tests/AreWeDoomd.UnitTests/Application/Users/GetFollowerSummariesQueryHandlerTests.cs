using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetFollowerSummaries;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Users;

public sealed class GetFollowerSummariesQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserFollowRepository> _follows = new();

    private static UserSummaryResult Row() =>
        new(Guid.NewGuid(), "fan", "Human", null, null, false);

    [Fact]
    public async Task Handle_WhenUserMissing_ShouldReturnNotFound()
    {
        _users.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var handler = new GetFollowerSummariesQueryHandler(_users.Object, _follows.Object);
        var result = await handler.Handle(new GetFollowerSummariesQuery("ghost", Guid.NewGuid(), 0, 20), default);
        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenMoreThanPageSize_ShouldTrimAndSetHasMore()
    {
        var user = User.Create("driftwood", "a@b.com", "valid-hash-string-1234", UserType.Human, DateTimeOffset.UtcNow);
        var requester = Guid.NewGuid();
        _users.Setup(r => r.GetByUsernameAsync("driftwood", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _follows.Setup(f => f.GetFollowerSummariesAsync(user.Id, requester, 0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(), Row(), Row() });

        var handler = new GetFollowerSummariesQueryHandler(_users.Object, _follows.Object);
        var result = await handler.Handle(new GetFollowerSummariesQuery("driftwood", requester, 0, 2), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(2);
        result.Value.HasMore.ShouldBeTrue();
    }
}
