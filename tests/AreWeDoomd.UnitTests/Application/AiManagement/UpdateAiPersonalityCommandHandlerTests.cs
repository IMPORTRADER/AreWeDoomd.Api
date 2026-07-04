using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Commands.UpdateAiPersonality;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class UpdateAiPersonalityCommandHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    public UpdateAiPersonalityCommandHandlerTests()
    {
        _dateTimeProvider.Setup(d => d.UtcNow).Returns(Now);
    }

    private UpdateAiPersonalityCommandHandler CreateHandler() =>
        new(_users.Object, _unitOfWork.Object, _dateTimeProvider.Object);

    private static UpdateAiPersonalityCommand ValidCommand(Guid userId) =>
        new(userId, ["curious", "witty"], "gen-z casual", "A curious and witty AI agent.");

    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsNotFound()
    {
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(ValidCommand(Guid.NewGuid()), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("ai_user.not_found");
    }

    [Fact]
    public async Task Handle_WhenUserIsHuman_ReturnsNotFound()
    {
        var human = User.Create("doga", "doga@test.com", "valid-hash-string-1234", UserType.Human, Now);
        _users.Setup(r => r.GetByIdAsync(human.Id, It.IsAny<CancellationToken>())).ReturnsAsync(human);

        var result = await CreateHandler().Handle(ValidCommand(human.Id), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("ai_user.not_found");
    }

    [Fact]
    public async Task Handle_WhenAiUser_BumpsVersionAndPersists()
    {
        var ai = User.Create("botty", "botty@ai.test", "valid-hash-string-1234", UserType.Ai, Now);
        ai.SetAiPersonality(["calm"], "quiet", "A calm bot.", Now);

        _users.Setup(r => r.GetByIdAsync(ai.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ai);

        var cmd = new UpdateAiPersonalityCommand(
            ai.Id,
            ["curious", "witty"],
            "gen-z casual",
            "A curious and witty AI agent.");

        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.PersonaVersion.ShouldBe(2);
        result.Value.Traits.ShouldBe((string[])["curious", "witty"]);
        result.Value.TypingStyle.ShouldBe("gen-z casual");
        result.Value.Summary.ShouldBe("A curious and witty AI agent.");

        _users.Verify(r => r.UpdateAsync(ai, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenElevenTraits_ShouldFail()
    {
        var validator = new UpdateAiPersonalityCommandValidator();
        var cmd = new UpdateAiPersonalityCommand(
            Guid.NewGuid(),
            ["t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8", "t9", "t10", "t11"],
            "style",
            "summary");

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateAiPersonalityCommand.Traits));
    }
}
