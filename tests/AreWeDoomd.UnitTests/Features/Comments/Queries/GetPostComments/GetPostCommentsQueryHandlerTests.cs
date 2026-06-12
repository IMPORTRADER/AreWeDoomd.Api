using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Domain.Posts;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Comments.Queries.GetPostComments;

public sealed class GetPostCommentsQueryHandlerTests
{
    private static readonly Guid PostId = Guid.NewGuid();

    private readonly Mock<IPostRepository> _postRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly GetPostCommentsQueryHandler _handler;

    public GetPostCommentsQueryHandlerTests()
    {
        _handler = new GetPostCommentsQueryHandler(
            _postRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenPostNotFound_ShouldReturnNotFound()
    {
        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, true), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("post.not_found");
    }

    [Fact]
    public async Task Handle_WithoutCursor_ShouldUsePlainListingWithSortAndLimit()
    {
        SetupPostExists();
        SetupCount(10);
        var comments = CreateComments(6);

        _commentRepositoryMock
            .Setup(r => r.GetPageByPostIdAsync(PostId, 6, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);
        SetupExists(older: true, newer: false);

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, true, CommentSortDirection.Desc, Limit: 6),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Comments.Count.ShouldBe(6);
        result.Value.TotalCount.ShouldBe(10);
        result.Value.HasMoreBefore.ShouldBeTrue();
        result.Value.HasMoreAfter.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_WhenGuest_ShouldCapLimitAtGuestMaximum()
    {
        SetupPostExists();
        SetupCount(10);

        _commentRepositoryMock
            .Setup(r => r.GetPageByPostIdAsync(PostId, 2, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateComments(2));
        SetupExists(older: false, newer: true);

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, IncludeAllComments: false, Limit: 50),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _commentRepositoryMock.Verify(
            r => r.GetPageByPostIdAsync(PostId, 2, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnchor_ShouldComposeOlderAndFromAnchorPages()
    {
        SetupPostExists();
        SetupCount(40);
        var anchorId = Guid.NewGuid();
        var cursor = new CommentCursor(DateTimeOffset.UtcNow, anchorId);

        _commentRepositoryMock
            .Setup(r => r.GetCursorAsync(PostId, anchorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor);
        _commentRepositoryMock
            .Setup(r => r.GetOlderThanAsync(PostId, cursor, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateComments(2));
        _commentRepositoryMock
            .Setup(r => r.GetFromAnchorAsync(PostId, cursor, 28, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateComments(28));
        SetupExists(older: true, newer: true);

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, true, Anchor: anchorId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Comments.Count.ShouldBe(30);
    }

    [Fact]
    public async Task Handle_WhenAnchorNotFound_ShouldFallBackToPlainListing()
    {
        SetupPostExists();
        SetupCount(5);
        var anchorId = Guid.NewGuid();

        _commentRepositoryMock
            .Setup(r => r.GetCursorAsync(PostId, anchorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommentCursor?)null);
        _commentRepositoryMock
            .Setup(r => r.GetPageByPostIdAsync(PostId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateComments(5));
        SetupExists(older: false, newer: false);

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, true, Anchor: anchorId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Comments.Count.ShouldBe(5);
        _commentRepositoryMock.Verify(
            r => r.GetPageByPostIdAsync(PostId, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithBeforeCursor_ShouldFetchOlderPage()
    {
        SetupPostExists();
        SetupCount(40);
        var beforeId = Guid.NewGuid();
        var cursor = new CommentCursor(DateTimeOffset.UtcNow, beforeId);

        _commentRepositoryMock
            .Setup(r => r.GetCursorAsync(PostId, beforeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor);
        _commentRepositoryMock
            .Setup(r => r.GetOlderThanAsync(PostId, cursor, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateComments(30));
        SetupExists(older: true, newer: true);

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, true, Before: beforeId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _commentRepositoryMock.Verify(
            r => r.GetOlderThanAsync(PostId, cursor, 30, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoComments_ShouldReturnEmptyWithNoMoreFlags()
    {
        SetupPostExists();
        SetupCount(0);

        _commentRepositoryMock
            .Setup(r => r.GetPageByPostIdAsync(PostId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommentResult>());

        var result = await _handler.Handle(
            new GetPostCommentsQuery(PostId, true), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Comments.ShouldBeEmpty();
        result.Value.HasMoreBefore.ShouldBeFalse();
        result.Value.HasMoreAfter.ShouldBeFalse();
    }

    private void SetupPostExists()
    {
        _postRepositoryMock
            .Setup(r => r.GetByIdAsync(PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePost());
    }

    private void SetupCount(int count)
    {
        _commentRepositoryMock
            .Setup(r => r.CountByPostIdAsync(PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
    }

    private void SetupExists(bool older, bool newer)
    {
        _commentRepositoryMock
            .Setup(r => r.ExistsOlderAsync(PostId, It.IsAny<CommentCursor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(older);
        _commentRepositoryMock
            .Setup(r => r.ExistsNewerAsync(PostId, It.IsAny<CommentCursor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newer);
    }

    private static IReadOnlyList<CommentResult> CreateComments(int count)
    {
        var baseTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        return Enumerable.Range(0, count)
            .Select(i => new CommentResult(
                Guid.NewGuid(),
                PostId,
                new PostAuthorResult(Guid.NewGuid(), $"user{i}", "Human", null),
                $"comment {i}",
                0,
                baseTime.AddMinutes(i),
                null))
            .ToList();
    }

    private static Post CreatePost()
    {
        return Post.Create(Guid.NewGuid(), "test post content", DateTimeOffset.UtcNow);
    }
}
