using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Commands.CreateAiUser;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class CreateAiUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");

    private readonly Mock<IAiAccountFactory> _factory = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    public CreateAiUserCommandHandlerTests()
    {
        _dateTimeProvider.Setup(d => d.UtcNow).Returns(Now);
    }

    private CreateAiUserCommandHandler CreateHandler() =>
        new(_factory.Object, _unitOfWork.Object, _dateTimeProvider.Object);

    private static CreateAiUserCommand ValidCommand(string? email = null) =>
        new("botty", email, ["curious", "witty"], "gen-z casual", "A curious and witty AI agent.");

    [Fact]
    public async Task Handle_WhenFactoryReturnsConflict_ReturnsConflict_AndNeverSaves()
    {
        _factory.Setup(f => f.CreateAiAccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Conflict("auth.username_taken", "Username is already taken."));

        var result = await CreateHandler().Handle(ValidCommand(), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("auth.username_taken");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailIsNull_UsesSyntheticEmail()
    {
        var ai = User.Create("botty", "botty@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);
        _factory.Setup(f => f.CreateAiAccountAsync(
                "botty", "botty@ai.arewedoomd.local", It.IsAny<string>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(ai));

        var result = await CreateHandler().Handle(ValidCommand(email: null), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Email.ShouldBe("botty@ai.arewedoomd.local");
    }

    [Fact]
    public async Task Handle_WhenEmailProvided_UsesProvidedEmail()
    {
        var ai = User.Create("botty", "custom@example.com", "valid-hash-string-1234", UserType.Ai, Now);
        _factory.Setup(f => f.CreateAiAccountAsync(
                "botty", "custom@example.com", It.IsAny<string>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(ai));

        var result = await CreateHandler().Handle(ValidCommand(email: "custom@example.com"), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Email.ShouldBe("custom@example.com");
    }

    [Fact]
    public async Task Handle_WhenSuccess_SetsPersonality_AndReturnsDetailWithVersion1()
    {
        var ai = User.Create("botty", "botty@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);
        _factory.Setup(f => f.CreateAiAccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(ai));

        var result = await CreateHandler().Handle(ValidCommand(), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasPersonality.ShouldBeTrue();
        result.Value.PersonaVersion.ShouldBe(1);
        result.Value.Traits.ShouldBe((string[])["curious", "witty"]);
        result.Value.TypingStyle.ShouldBe("gen-z casual");
        result.Value.Summary.ShouldBe("A curious and witty AI agent.");
    }

    [Fact]
    public async Task Handle_WhenSuccess_SavesExactlyOnce()
    {
        var ai = User.Create("botty", "botty@ai.arewedoomd.local", "valid-hash-string-1234", UserType.Ai, Now);
        _factory.Setup(f => f.CreateAiAccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(ai));

        await CreateHandler().Handle(ValidCommand(), default);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenBadUsername_ShouldFail()
    {
        var validator = new CreateAiUserCommandValidator();
        var cmd = new CreateAiUserCommand("ab", null, ["curious"], "style", "summary");

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateAiUserCommand.Username));
    }

    [Fact]
    public void Validator_WhenElevenTraits_ShouldFail()
    {
        var validator = new CreateAiUserCommandValidator();
        var cmd = new CreateAiUserCommand(
            "botty", null,
            ["t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8", "t9", "t10", "t11"],
            "style", "summary");

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateAiUserCommand.Traits));
    }

    [Fact]
    public void Validator_WhenWhitespaceSummary_ShouldFail()
    {
        var validator = new CreateAiUserCommandValidator();
        var cmd = new CreateAiUserCommand("botty", null, ["curious", "witty"], "gen-z casual", "   ");

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateAiUserCommand.Summary));
    }
}
