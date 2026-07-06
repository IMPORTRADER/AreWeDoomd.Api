using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.AiManagement.Queries.ListAiUsers;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class ListAiUsersQueryHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");
    private readonly Mock<IAiUserReadRepository> _repo = new();

    private ListAiUsersQueryHandler CreateHandler() => new(_repo.Object);

    private static AiUserListItem MakeItem(string username) =>
        new(Guid.NewGuid(), username, null, Now, false, [], null, null);

    [Fact]
    public async Task Handle_HappyPath_ReturnsMappedItemsAndCorrectHasMore()
    {
        var items = new List<AiUserListItem> { MakeItem("bot1"), MakeItem("bot2") };
        _repo.Setup(r => r.ListAsync(null, null, 0, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<AiUserListItem>)items, 5));

        var result = await CreateHandler().Handle(new ListAiUsersQuery(null, null, 0, 2), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(5);
        result.Value.HasMore.ShouldBeTrue(); // 0 + 2 < 5
    }

    [Fact]
    public async Task Handle_WhenAllItemsReturned_HasMoreIsFalse()
    {
        var items = new List<AiUserListItem> { MakeItem("bot1"), MakeItem("bot2") };
        _repo.Setup(r => r.ListAsync(null, null, 0, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<AiUserListItem>)items, 2));

        var result = await CreateHandler().Handle(new ListAiUsersQuery(null, null, 0, 2), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasMore.ShouldBeFalse(); // 0 + 2 == 2, not less
    }

    [Fact]
    public async Task Handle_WhenPageSizeExceedsMax_ClampsTo100()
    {
        var items = new List<AiUserListItem> { MakeItem("bot1") };
        _repo.Setup(r => r.ListAsync(null, null, 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<AiUserListItem>)items, 1));

        var result = await CreateHandler().Handle(new ListAiUsersQuery(null, null, 0, 500), default);

        result.IsSuccess.ShouldBeTrue();
        _repo.Verify(r => r.ListAsync(null, null, 0, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOffsetNegative_FloorsToZero()
    {
        var items = new List<AiUserListItem> { MakeItem("bot1") };
        _repo.Setup(r => r.ListAsync(null, null, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<AiUserListItem>)items, 1));

        var result = await CreateHandler().Handle(new ListAiUsersQuery(null, null, -5, 10), default);

        result.IsSuccess.ShouldBeTrue();
        _repo.Verify(r => r.ListAsync(null, null, 0, 10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
