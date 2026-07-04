using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetAiUserDetailQueryHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");
    private readonly Mock<IUserRepository> _users = new();

    private GetAiUserDetailQueryHandler CreateHandler() => new(_users.Object);

    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsNotFound()
    {
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(new GetAiUserDetailQuery(Guid.NewGuid()), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("ai_user.not_found");
    }

    [Fact]
    public async Task Handle_WhenUserIsHuman_ReturnsNotFound()
    {
        var human = User.Create("doga", "doga@test.com", "valid-hash-string-1234", UserType.Human, Now);
        _users.Setup(r => r.GetByIdAsync(human.Id, It.IsAny<CancellationToken>())).ReturnsAsync(human);

        var result = await CreateHandler().Handle(new GetAiUserDetailQuery(human.Id), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("ai_user.not_found");
    }

    [Fact]
    public async Task Handle_WhenAiHasPersonality_ReturnsMappedResult()
    {
        var ai = User.Create("botty", "botty@ai.test", "valid-hash-string-1234", UserType.Ai, Now);
        ai.SetAiPersonality(["toxic", "flirty"], "gen-z", "Chaos gremlin.", Now);
        _users.Setup(r => r.GetByIdAsync(ai.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ai);

        var result = await CreateHandler().Handle(new GetAiUserDetailQuery(ai.Id), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasPersonality.ShouldBeTrue();
        result.Value.Traits.ShouldBe((string[])["toxic", "flirty"]);
        result.Value.TypingStyle.ShouldBe("gen-z");
        result.Value.Summary.ShouldBe("Chaos gremlin.");
        result.Value.PersonaVersion.ShouldBe(1);
        result.Value.PersonaUpdatedAt.ShouldBe(Now);
        result.Value.Id.ShouldBe(ai.Id);
        result.Value.Username.ShouldBe("botty");
        result.Value.Email.ShouldBe("botty@ai.test");
    }

    [Fact]
    public async Task Handle_WhenAiHasNoPersonality_ReturnsSuccessWithNullPersonaFields()
    {
        var ai = User.Create("botty", "botty@ai.test", "valid-hash-string-1234", UserType.Ai, Now);
        _users.Setup(r => r.GetByIdAsync(ai.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ai);

        var result = await CreateHandler().Handle(new GetAiUserDetailQuery(ai.Id), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasPersonality.ShouldBeFalse();
        result.Value.Traits.ShouldBeEmpty();
        result.Value.TypingStyle.ShouldBeNull();
        result.Value.Summary.ShouldBeNull();
        result.Value.PersonaVersion.ShouldBeNull();
        result.Value.PersonaUpdatedAt.ShouldBeNull();
    }
}
