using AreWeDoomd.Api.Startup;
using AreWeDoomd.UnitTests.ChatProviders;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Api.Startup;

public class DatabasePreflightTests
{
    private static readonly IReadOnlyList<TimeSpan> TwoDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    [Fact]
    public async Task ExecuteAsync_WhenFirstAttemptSucceeds_ShouldReturnTrueWithoutDelaying()
    {
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger();

        var result = await DatabasePreflight.ExecuteAsync(
            _ => Task.FromResult(true),
            TwoDelays,
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            logger);

        result.ShouldBeTrue();
        delays.ShouldBeEmpty();
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSecondAttemptSucceeds_ShouldWaitFirstDelayAndLogWarning()
    {
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger();
        var attempts = 0;

        var result = await DatabasePreflight.ExecuteAsync(
            _ => Task.FromResult(++attempts >= 2),
            TwoDelays,
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            logger);

        result.ShouldBeTrue();
        delays.ShouldBe(new[] { TimeSpan.FromSeconds(1) });
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(1);
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllAttemptsFail_ShouldUseEveryDelayThenReturnFalseAndLogError()
    {
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger();

        var result = await DatabasePreflight.ExecuteAsync(
            _ => Task.FromResult(false),
            TwoDelays,
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            logger);

        result.ShouldBeFalse();
        delays.ShouldBe(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) });
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(2);
        logger.Entries.Count(e => e.Level == LogLevel.Error).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProbeThrows_ShouldTreatAsFailedAttemptAndRetry()
    {
        var logger = new CapturingLogger();
        var attempts = 0;

        var result = await DatabasePreflight.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("db is waking up");
                }
                return Task.FromResult(true);
            },
            TwoDelays,
            (_, _) => Task.CompletedTask,
            logger);

        result.ShouldBeTrue();
        attempts.ShouldBe(2);
        logger.Entries.Single(e => e.Level == LogLevel.Warning).Message.ShouldContain("db is waking up");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoDelaysConfigured_ShouldTryExactlyOnce()
    {
        var logger = new CapturingLogger();
        var attempts = 0;

        var result = await DatabasePreflight.ExecuteAsync(
            _ => { attempts++; return Task.FromResult(false); },
            Array.Empty<TimeSpan>(),
            (_, _) => Task.CompletedTask,
            logger);

        result.ShouldBeFalse();
        attempts.ShouldBe(1);
        logger.Entries.Count(e => e.Level == LogLevel.Error).ShouldBe(1);
    }
}
