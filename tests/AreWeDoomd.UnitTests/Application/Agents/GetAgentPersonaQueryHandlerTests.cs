using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Agents.Queries.GetAgentPersona;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Agents;

public sealed class GetAgentPersonaQueryHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");
    private readonly Mock<IUserRepository> _users = new();

    private GetAgentPersonaQueryHandler CreateHandler() => new(_users.Object);

    [Fact]
    public async Task Handle_WhenUserMissing_ShouldReturnNotFound()
    {
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new GetAgentPersonaQuery(Guid.NewGuid()), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenUserIsHuman_ShouldReturnNotFound()
    {
        var human = User.Create("doga", "doga@test.com", "valid-hash-string-1234", UserType.Human, Now);
        _users.Setup(r => r.GetByIdAsync(human.Id, It.IsAny<CancellationToken>())).ReturnsAsync(human);

        var result = await CreateHandler().Handle(new GetAgentPersonaQuery(human.Id), default);

        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAiHasNoPersonality_ShouldReturnNotFound()
    {
        var ai = User.Create("botty", "botty@ai.test", "valid-hash-string-1234", UserType.Ai, Now);
        _users.Setup(r => r.GetByIdAsync(ai.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ai);

        var result = await CreateHandler().Handle(new GetAgentPersonaQuery(ai.Id), default);

        result.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAiHasPersonality_ShouldReturnMappedResult()
    {
        var ai = User.Create("botty", "botty@ai.test", "valid-hash-string-1234", UserType.Ai, Now);
        ai.SetAiPersonality(["toxic", "flirty"], "gen-z", "Chaos gremlin.", Now);
        _users.Setup(r => r.GetByIdAsync(ai.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ai);

        var result = await CreateHandler().Handle(new GetAgentPersonaQuery(ai.Id), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Traits.ShouldBe((string[])["toxic", "flirty"]);
        result.Value.TypingStyle.ShouldBe("gen-z");
        result.Value.Summary.ShouldBe("Chaos gremlin.");
        result.Value.Version.ShouldBe(1);
        result.Value.UserId.ShouldBe(ai.Id);
    }
}
