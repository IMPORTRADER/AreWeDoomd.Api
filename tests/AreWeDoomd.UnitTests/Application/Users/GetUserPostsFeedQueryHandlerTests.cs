using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetUserPostsFeed;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Users;

public sealed class GetUserPostsFeedQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFeedRepository> _feed = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    private static FeedPostResult Post(Guid id) =>
        new(id, new PostAuthorResult(Guid.NewGuid(), "u", "Human", null), "c", 0, 0, 0,
            Array.Empty<AreWeDoomd.Application.Features.Comments.Common.CommentResult>(),
            DateTimeOffset.UtcNow, null);

    [Fact]
    public async Task Handle_WhenMoreThanPageSize_ShouldTrimAndSetHasMoreTrue()
    {
        var user = User.Create("driftwood", "a@b.com", "valid-hash-string-1234", UserType.Human, DateTimeOffset.UtcNow);
        var asOf = DateTimeOffset.UtcNow;
        _clock.SetupGet(c => c.UtcNow).Returns(asOf);
        _users.Setup(r => r.GetByUsernameAsync("driftwood", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        // pageSize 2 → handler asks for 3; repo returns 3 → hasMore true, items trimmed to 2
        _feed.Setup(r => r.GetUserPostsAsync(user.Id, asOf, 0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Post(Guid.NewGuid()), Post(Guid.NewGuid()), Post(Guid.NewGuid()) });

        var handler = new GetUserPostsFeedQueryHandler(_users.Object, _feed.Object, _clock.Object);
        var result = await handler.Handle(new GetUserPostsFeedQuery("driftwood", null, 0, 2), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Posts.Count.ShouldBe(2);
        result.Value.HasMore.ShouldBeTrue();
        result.Value.AsOf.ShouldBe(asOf);
    }

    [Fact]
    public async Task Handle_WhenUserMissing_ShouldReturnNotFound()
    {
        _users.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var handler = new GetUserPostsFeedQueryHandler(_users.Object, _feed.Object, _clock.Object);
        var result = await handler.Handle(new GetUserPostsFeedQuery("ghost", null, 0, 20), default);
        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }
}
