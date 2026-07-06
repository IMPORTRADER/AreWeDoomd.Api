using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.ChatProviders;

public class ChatProviderStartupSummaryTests
{
    private static IConfiguration BuildConfiguration(string? geminiKey, string? openRouterKey)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ChatProviders:Gemini:ApiKey"] = geminiKey,
                ["ChatProviders:OpenRouter:ApiKey"] = openRouterKey
            })
            .Build();
    }

    [Fact]
    public void LogSummary_WhenAllProvidersHaveKeys_ShouldLogNoWarningsOrErrors()
    {
        var configuration = BuildConfiguration("gk", "ok");
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "gemini", logger);

        result.ShouldBeTrue();
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Warning);
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void LogSummary_WhenOneProviderMissingKey_ShouldLogWarningForThatProvider()
    {
        var configuration = BuildConfiguration("gk", null);
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "gemini", logger);

        result.ShouldBeTrue();
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(1);
        logger.Entries.Single(e => e.Level == LogLevel.Warning).Message.ShouldContain("openrouter");
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void LogSummary_WhenNoProviderHasKey_ShouldLogError()
    {
        var configuration = BuildConfiguration(null, null);
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "gemini", logger);

        result.ShouldBeFalse();
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(2);
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void LogSummary_WhenSelectedProviderMissingKey_ShouldReturnFalseAndLogError()
    {
        var configuration = BuildConfiguration("gk", null);
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "openrouter", logger);

        result.ShouldBeFalse();
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Message.Contains("openrouter"));
    }

    [Fact]
    public void IsProviderConfigured_WhenKeyPresent_ShouldReturnTrue()
    {
        var configuration = BuildConfiguration("gk", null);

        ChatProviderStartupSummary.IsProviderConfigured(configuration, "gemini").ShouldBeTrue();
    }

    [Fact]
    public void IsProviderConfigured_WhenKeyMissingOrProviderUnknown_ShouldReturnFalse()
    {
        var configuration = BuildConfiguration("gk", null);

        ChatProviderStartupSummary.IsProviderConfigured(configuration, "openrouter").ShouldBeFalse();
        ChatProviderStartupSummary.IsProviderConfigured(configuration, "no-such-provider").ShouldBeFalse();
    }
}
