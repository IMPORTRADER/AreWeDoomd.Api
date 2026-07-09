using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.LlmSettings.Commands.UpdateLlmSettings;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.LlmSettings;

public class UpdateLlmSettingsCommandValidatorTests
{
    private static UpdateLlmSettingsCommandValidator CreateValidator()
    {
        var catalog = new Mock<IChatProviderCatalog>();
        catalog.Setup(c => c.Exists("openrouter")).Returns(true);
        catalog.Setup(c => c.Exists("nope")).Returns(false);
        return new UpdateLlmSettingsCommandValidator(catalog.Object);
    }

    private static UpdateLlmSettingsCommand ValidCommand(string? provider) =>
        new("model-x", "", false, provider, 512, 800, 1024);

    [Fact]
    public void Validate_WhenProviderIsRegistered_ShouldBeValid()
    {
        var result = CreateValidator().Validate(ValidCommand("openrouter"));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenProviderIsEmpty_ShouldBeValid()
    {
        var result = CreateValidator().Validate(ValidCommand(""));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenProviderIsUnknown_ShouldBeInvalid()
    {
        var result = CreateValidator().Validate(ValidCommand("nope"));
        result.IsValid.ShouldBeFalse();
    }
}
