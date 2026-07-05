using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;
using Moq;
using Shouldly;
using Xunit;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.UnitTests.Features.LlmSettings;

public sealed class UpdateLlmSettingsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILlmSettingsRepository> _repo = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly UpdateLlmSettingsCommandHandler _handler;

    public UpdateLlmSettingsCommandHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _handler = new UpdateLlmSettingsCommandHandler(_repo.Object, _clock.Object, _uow.Object);
    }

    private static UpdateLlmSettingsCommand ValidCommand() => new(
        Model: "openai/gpt-oss-120b:free", ScoringModel: "", ThinkingEnabled: false,
        ScoringTokensPerAccount: 512, CompositionTokensPerPost: 800,
        PersonaTokensPerPersona: 700, ReplyMaxTokens: 1024);

    [Fact]
    public async Task Handle_WhenNoRow_ShouldCreateDefaultThenUpdateAndSave()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DomainLlmSettings?)null);

        var result = await _handler.Handle(ValidCommand() with { ThinkingEnabled = true }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ThinkingEnabled.ShouldBeTrue();
        _repo.Verify(r => r.AddAsync(It.IsAny<DomainLlmSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRowExists_ShouldUpdateWithoutAdding()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainLlmSettings.CreateDefault(Now));

        var result = await _handler.Handle(ValidCommand() with { ReplyMaxTokens = 2048 }, CancellationToken.None);

        result.Value!.ReplyMaxTokens.ShouldBe(2048);
        _repo.Verify(r => r.AddAsync(It.IsAny<DomainLlmSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
