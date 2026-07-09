using AreWeDoomd.Domain.Ai;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Ai;

public sealed class LlmSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDefault_ShouldUseFreeOpenRouterModelAndSafeBudgets()
    {
        var settings = LlmSettings.CreateDefault(Now);

        settings.Id.ShouldBe(LlmSettings.SingletonId);
        settings.Model.ShouldBe("openai/gpt-oss-120b:free");
        settings.ScoringModel.ShouldBe(string.Empty);
        settings.ThinkingEnabled.ShouldBeFalse();
        settings.ScoringTokensPerAccount.ShouldBe(512);
        settings.CompositionTokensPerPost.ShouldBe(800);
        settings.ReplyMaxTokens.ShouldBe(1024);
        settings.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Update_ShouldApplyAllFields()
    {
        var settings = LlmSettings.CreateDefault(Now);
        var later = Now.AddHours(1);

        settings.Update("anthropic/claude-haiku-4.5", "meta-llama/llama-3.3-70b-instruct:free",
            thinkingEnabled: true, scoringTokensPerAccount: 256, compositionTokensPerPost: 1200,
            replyMaxTokens: 2048, now: later);

        settings.Model.ShouldBe("anthropic/claude-haiku-4.5");
        settings.ScoringModel.ShouldBe("meta-llama/llama-3.3-70b-instruct:free");
        settings.ThinkingEnabled.ShouldBeTrue();
        settings.ScoringTokensPerAccount.ShouldBe(256);
        settings.CompositionTokensPerPost.ShouldBe(1200);
        settings.ReplyMaxTokens.ShouldBe(2048);
        settings.UpdatedAt.ShouldBe(later);
    }

    [Theory]
    [InlineData(127)]
    [InlineData(8193)]
    public void Update_WhenBudgetOutOfRange_ShouldThrow(int budget)
    {
        var settings = LlmSettings.CreateDefault(Now);

        Should.Throw<ArgumentOutOfRangeException>(() => settings.Update(
            "m", "", false, budget, 800, 1024, Now));
        Should.Throw<ArgumentOutOfRangeException>(() => settings.Update(
            "m", "", false, 512, budget, 1024, Now));
        Should.Throw<ArgumentOutOfRangeException>(() => settings.Update(
            "m", "", false, 512, 800, budget, Now));
    }

    [Fact]
    public void Update_WhenModelBlank_ShouldThrow()
    {
        var settings = LlmSettings.CreateDefault(Now);

        Should.Throw<ArgumentException>(() => settings.Update(
            "  ", "", false, 512, 800, 1024, Now));
    }
}
