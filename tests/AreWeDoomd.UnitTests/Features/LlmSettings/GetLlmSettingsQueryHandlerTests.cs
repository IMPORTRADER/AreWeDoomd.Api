using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.LlmSettings.Queries.GetLlmSettings;
using Moq;
using Shouldly;
using Xunit;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.UnitTests.Features.LlmSettings;

public sealed class GetLlmSettingsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILlmSettingsRepository> _repo = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly GetLlmSettingsQueryHandler _handler;

    public GetLlmSettingsQueryHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _handler = new GetLlmSettingsQueryHandler(_repo.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoRow_ShouldReturnDefaults()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DomainLlmSettings?)null);

        var result = await _handler.Handle(new GetLlmSettingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Model.ShouldBe("openai/gpt-oss-120b:free");
        result.Value.ThinkingEnabled.ShouldBeFalse();
        result.Value.ReplyMaxTokens.ShouldBe(1024);
    }

    [Fact]
    public async Task Handle_WhenRowExists_ShouldMapAllFields()
    {
        var settings = DomainLlmSettings.CreateDefault(Now);
        settings.Update("anthropic/claude-haiku-4.5", "x", true, provider: string.Empty, 256, 900, 2048, Now);
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);

        var result = await _handler.Handle(new GetLlmSettingsQuery(), CancellationToken.None);

        result.Value!.Model.ShouldBe("anthropic/claude-haiku-4.5");
        result.Value.ScoringModel.ShouldBe("x");
        result.Value.ThinkingEnabled.ShouldBeTrue();
        result.Value.ScoringTokensPerAccount.ShouldBe(256);
        result.Value.CompositionTokensPerPost.ShouldBe(900);
        result.Value.ReplyMaxTokens.ShouldBe(2048);
    }
}
