using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.Infrastructure.Ai;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class ChatPersonaGeneratorTests
{
    private static readonly PersonaGenerationOptions DefaultOptions = new()
    {
        Provider = "openrouter",
        Model = "test-model",
        BatchSize = 10,
        MaxCount = 50
    };

    private static ChatPersonaGenerator CreateGenerator(
        IChatProvider provider,
        PersonaGenerationOptions? options = null,
        bool isConfigured = true)
    {
        return new ChatPersonaGenerator(
            provider,
            options ?? DefaultOptions,
            NullLogger<ChatPersonaGenerator>.Instance,
            isConfigured);
    }

    private static ChatResult OkResult(string json) =>
        ChatResult.Ok(json, new TokenUsage(10, 20), FinishReason.Stop);

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateBatchAsync_WhenProviderReturnsValidJson_ReturnsSuccessWithParsedPersonas()
    {
        const string json = """
        {
          "personas": [
            {
              "username": "cool_bot",
              "traits": ["sarcastic", "clever"],
              "typingStyle": "Uses short punchy sentences.",
              "summary": "A witty AI that loves debate."
            }
          ]
        }
        """;

        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);

        var result = await generator.GenerateBatchAsync(1, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);

        var persona = result.Value[0];
        persona.Username.ShouldBe("cool_bot");
        persona.Traits.ShouldBe(["sarcastic", "clever"]);
        persona.TypingStyle.ShouldBe("Uses short punchy sentences.");
        persona.Summary.ShouldBe("A witty AI that loves debate.");
    }

    [Fact]
    public async Task GenerateBatchAsync_WhenProviderReturnsValidJson_UsesPersonaBatchSchemaAndTemperature()
    {
        const string json = """
        {
          "personas": [
            {
              "username": "neat_user",
              "traits": ["calm"],
              "typingStyle": "Writes in full sentences.",
              "summary": "A composed and helpful bot."
            }
          ]
        }
        """;

        ChatRequest? capturedRequest = null;
        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ChatRequest, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);
        await generator.GenerateBatchAsync(1, CancellationToken.None);

        capturedRequest.ShouldNotBeNull();
        capturedRequest!.JsonResponseSchema.ShouldBe(PersonaBatchSchema.Json);
        capturedRequest.Temperature.ShouldBe(1.0);
        capturedRequest.Model.ShouldBe(DefaultOptions.Model);
    }

    // ── dirty payload ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateBatchAsync_WithDirtyPayload_CleansUsername_DropsLongTrait_DropsPersonaWithBlankSummary()
    {
        // Persona 1: username has invalid chars + spaces → cleaned; one trait is 70 chars → dropped
        // Persona 2: blank summary → entire persona dropped
        const string json = """
        {
          "personas": [
            {
              "username": "Hello World! 123",
              "traits": ["fine-trait", "this-trait-is-way-too-long-and-exceeds-the-sixty-character-maximum-limit!!"],
              "typingStyle": "Speaks casually.",
              "summary": "An AI persona with a messy username."
            },
            {
              "username": "cleanguy",
              "traits": ["helpful"],
              "typingStyle": "Formal and concise.",
              "summary": ""
            }
          ]
        }
        """;

        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(2, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // Persona 2 dropped (blank summary); only persona 1 survives
        result.Value!.Count.ShouldBe(1);

        var persona = result.Value[0];
        // "Hello World! 123" → strip non-[a-z0-9_], lowercase → "helloworld123" (spaces and ! stripped)
        persona.Username.ShouldBe("helloworld123");
        // 70-char trait dropped; only "fine-trait" remains
        persona.Traits.Count.ShouldBe(1);
        persona.Traits[0].ShouldBe("fine-trait");
    }

    [Fact]
    public async Task GenerateBatchAsync_WithUnsalvageableUsername_DropsEntirePersona()
    {
        // Username becomes empty after stripping → dropped
        const string json = """
        {
          "personas": [
            {
              "username": "!!!",
              "traits": ["valid"],
              "typingStyle": "Does things.",
              "summary": "A persona with unusable username."
            },
            {
              "username": "ok",
              "traits": ["ok"],
              "typingStyle": "Fine.",
              "summary": "Too short username — only 2 chars after strip."
            },
            {
              "username": "validone",
              "traits": ["normal"],
              "typingStyle": "Speaks normally.",
              "summary": "Good persona."
            }
          ]
        }
        """;

        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(3, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        // "!!!" → empty after strip → dropped
        // "ok" → 2 chars → dropped
        // "validone" → kept
        result.Value!.Count.ShouldBe(1);
        result.Value[0].Username.ShouldBe("validone");
    }

    [Fact]
    public async Task GenerateBatchAsync_WithPersonaHavingZeroValidTraits_DropsPersona()
    {
        const string json = """
        {
          "personas": [
            {
              "username": "notraits",
              "traits": ["x"],
              "typingStyle": "Some style.",
              "summary": "A persona whose only trait is < 2 chars."
            }
          ]
        }
        """;

        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(1, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GenerateBatchAsync_TraitsAreCappedAtTen()
    {
        var traits = Enumerable.Range(1, 12).Select(i => $"trait{i}").ToArray();
        var traitsJson = string.Join(",", traits.Select(t => $"\"{t}\""));

        var json = $$"""
        {
          "personas": [
            {
              "username": "manytraits",
              "traits": [{{traitsJson}}],
              "typingStyle": "Some style.",
              "summary": "A persona with many traits."
            }
          ]
        }
        """;

        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(1, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value[0].Traits.Count.ShouldBe(10);
    }

    [Fact]
    public async Task GenerateBatchAsync_UsernameLongerThan24Chars_IsTruncatedTo24()
    {
        const string json = """
        {
          "personas": [
            {
              "username": "averylongusernamethatiswellover24characters",
              "traits": ["witty"],
              "typingStyle": "Writes concisely.",
              "summary": "A persona with a long username."
            }
          ]
        }
        """;

        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult(json));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(1, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value![0].Username.Length.ShouldBe(24);
    }

    // ── provider failure ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateBatchAsync_WhenProviderFails_ReturnsFailureWithProviderMessage()
    {
        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChatResult.Fail(new ChatError("rate limit exceeded", 429, "openrouter")));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(5, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("persona.generation_failed");
        result.Error.Message.ShouldContain("rate limit exceeded");
    }

    // ── unparseable JSON ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateBatchAsync_WhenProviderReturnsInvalidJson_ReturnsFailure()
    {
        var provider = new Mock<IChatProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("this is not json at all"));

        var generator = CreateGenerator(provider.Object);
        var result = await generator.GenerateBatchAsync(5, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("persona.generation_failed");
    }

    // ── unconfigured ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IsConfigured_WhenFalse_ReturnsFalseAndGenerateBatchNeverCallsProvider()
    {
        var provider = new Mock<IChatProvider>(MockBehavior.Strict);
        // Strict mock: any call to CompleteAsync will throw → proves provider is never invoked

        var generator = CreateGenerator(provider.Object, isConfigured: false);

        generator.IsConfigured.ShouldBeFalse();

        var result = await generator.GenerateBatchAsync(5, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("persona.generator_unconfigured");
    }

    [Fact]
    public void IsConfigured_WhenTrue_ReturnsTrueFromProperty()
    {
        var provider = new Mock<IChatProvider>();
        var generator = CreateGenerator(provider.Object, isConfigured: true);
        generator.IsConfigured.ShouldBeTrue();
    }
}
