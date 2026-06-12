using AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Comments.Queries.GetPostComments;

public sealed class GetPostCommentsQueryValidatorTests
{
    private readonly GetPostCommentsQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WhenPostIdIsEmpty_ShouldFail()
    {
        var query = new GetPostCommentsQuery(Guid.Empty, true);
        var result = await _validator.ValidateAsync(query);
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validate_WhenLimitIsOutOfRange_ShouldFail(int limit)
    {
        var query = new GetPostCommentsQuery(Guid.NewGuid(), true, Limit: limit);
        var result = await _validator.ValidateAsync(query);
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task Validate_WhenAroundIsOutOfRange_ShouldFail(int around)
    {
        var query = new GetPostCommentsQuery(Guid.NewGuid(), true, Around: around);
        var result = await _validator.ValidateAsync(query);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_WhenMultipleCursorsProvided_ShouldFail()
    {
        var query = new GetPostCommentsQuery(
            Guid.NewGuid(), true, Anchor: Guid.NewGuid(), Before: Guid.NewGuid());
        var result = await _validator.ValidateAsync(query);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_WhenOnlyAnchorProvided_ShouldPass()
    {
        var query = new GetPostCommentsQuery(
            Guid.NewGuid(), true, Anchor: Guid.NewGuid(), Limit: 30);
        var result = await _validator.ValidateAsync(query);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_WithDefaults_ShouldPass()
    {
        var query = new GetPostCommentsQuery(Guid.NewGuid(), false);
        var result = await _validator.ValidateAsync(query);
        result.IsValid.ShouldBeTrue();
    }
}
