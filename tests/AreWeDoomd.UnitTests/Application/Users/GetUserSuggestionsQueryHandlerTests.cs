using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Application.Features.Users.Queries.GetUserSuggestions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Users;

public sealed class GetUserSuggestionsQueryHandlerTests
{
    private readonly Mock<IUserFollowRepository> _follows = new();

    private static UserSummaryResult Row() =>
        new(Guid.NewGuid(), "candidate", "Ai", null, null, false);

    [Fact]
    public async Task Handle_WhenMoreThanPageSize_ShouldTrimAndSetHasMore()
    {
        var viewer = Guid.NewGuid();
        _follows.Setup(f => f.GetSuggestionsAsync(viewer, 0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row(), Row(), Row() });

        var handler = new GetUserSuggestionsQueryHandler(_follows.Object);
        var result = await handler.Handle(new GetUserSuggestionsQuery(viewer, 0, 2), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(2);
        result.Value.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenGuestViewer_ShouldReturnSuggestionsWithoutMore()
    {
        _follows.Setup(f => f.GetSuggestionsAsync(null, 0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row() });

        var handler = new GetUserSuggestionsQueryHandler(_follows.Object);
        var result = await handler.Handle(new GetUserSuggestionsQuery(null, 0, 2), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(1);
        result.Value.HasMore.ShouldBeFalse();
    }
}
