using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Feed.Queries.GetGlobalFeed;

public sealed class GetGlobalFeedQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IFeedRepository> _feedRepositoryMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly GetGlobalFeedQueryHandler _handler;

    public GetGlobalFeedQueryHandlerTests()
    {
        _dateTimeProviderMock.SetupGet(p => p.UtcNow).Returns(Now);

        _feedRepositoryMock
            .Setup(r => r.AttachCommentsAsync(
                It.IsAny<IReadOnlyList<FeedPostResult>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<FeedPostResult> posts, bool _, CancellationToken _) => posts);

        _handler = new GetGlobalFeedQueryHandler(
            _feedRepositoryMock.Object,
            _dateTimeProviderMock.Object);
    }

    private static FeedPostResult Post(int likeCount, Guid? id = null)
    {
        return new FeedPostResult(
            id ?? Guid.NewGuid(),
            new PostAuthorResult(Guid.NewGuid(), "user", "Human", null),
            "content",
            likeCount,
            0,
            0,
            Array.Empty<CommentResult>(),
            Now.AddHours(-1),
            null);
    }

    private static List<FeedPostResult> Candidates(int count)
    {
        return Enumerable.Range(0, count).Select(i => Post(i)).ToList();
    }

    private void SetupWindowed(IReadOnlyList<FeedPostResult> result)
    {
        _feedRepositoryMock
            .Setup(r => r.GetGlobalCandidatesAsync(
                It.IsAny<DateTimeOffset>(),
                It.Is<DateTimeOffset?>(d => d.HasValue),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupFallback(IReadOnlyList<FeedPostResult> result)
    {
        _feedRepositoryMock
            .Setup(r => r.GetGlobalCandidatesAsync(
                It.IsAny<DateTimeOffset>(),
                It.Is<DateTimeOffset?>(d => !d.HasValue),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    [Fact]
    public async Task Handle_WhenAsOfNull_ShouldStampUtcNowAndReturnIt()
    {
        SetupWindowed(Candidates(25));

        var result = await _handler.Handle(
            new GetGlobalFeedQuery(true, null, 0, 20), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AsOf.ShouldBe(Now);
    }

    [Fact]
    public async Task Handle_WhenAsOfProvided_ShouldUseItAsReferenceTime()
    {
        var asOf = Now.AddHours(-3);
        SetupWindowed(Candidates(25));

        var result = await _handler.Handle(
            new GetGlobalFeedQuery(true, asOf, 0, 20), CancellationToken.None);

        result.Value!.AsOf.ShouldBe(asOf);
        _feedRepositoryMock.Verify(r => r.GetGlobalCandidatesAsync(
            asOf, It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEnoughCandidates_ShouldNotFallBack()
    {
        SetupWindowed(Candidates(25));

        await _handler.Handle(new GetGlobalFeedQuery(true, null, 0, 20), CancellationToken.None);

        _feedRepositoryMock.Verify(r => r.GetGlobalCandidatesAsync(
            It.IsAny<DateTimeOffset>(), It.Is<DateTimeOffset?>(d => !d.HasValue),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTooFewCandidates_ShouldFallBackToNoWindow()
    {
        SetupWindowed(Candidates(5));
        SetupFallback(Candidates(25));

        var result = await _handler.Handle(
            new GetGlobalFeedQuery(true, null, 0, 20), CancellationToken.None);

        result.Value!.Posts.Count.ShouldBe(20);
        _feedRepositoryMock.Verify(r => r.GetGlobalCandidatesAsync(
            It.IsAny<DateTimeOffset>(), It.Is<DateTimeOffset?>(d => !d.HasValue),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPageByOffsetAndPageSizeInRankedOrder()
    {
        SetupWindowed(Candidates(25));

        var page = await _handler.Handle(
            new GetGlobalFeedQuery(true, null, 0, 10), CancellationToken.None);

        page.Value!.Posts.Count.ShouldBe(10);
        page.Value.Posts[0].LikeCount.ShouldBe(24);
        page.Value.Posts[9].LikeCount.ShouldBe(15);
        page.Value.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenLastPage_ShouldReportHasMoreFalse()
    {
        SetupWindowed(Candidates(25));

        var page = await _handler.Handle(
            new GetGlobalFeedQuery(true, null, 20, 10), CancellationToken.None);

        page.Value!.Posts.Count.ShouldBe(5);
        page.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldClampPageSizeAndOffset()
    {
        SetupWindowed(Candidates(25));

        var page = await _handler.Handle(
            new GetGlobalFeedQuery(true, null, -5, 999), CancellationToken.None);

        page.Value!.Posts.Count.ShouldBe(25);
        page.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ShouldAttachCommentsOnlyToThePageSlice()
    {
        SetupWindowed(Candidates(25));

        await _handler.Handle(new GetGlobalFeedQuery(true, null, 0, 10), CancellationToken.None);

        _feedRepositoryMock.Verify(r => r.AttachCommentsAsync(
            It.Is<IReadOnlyList<FeedPostResult>>(p => p.Count == 10),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
