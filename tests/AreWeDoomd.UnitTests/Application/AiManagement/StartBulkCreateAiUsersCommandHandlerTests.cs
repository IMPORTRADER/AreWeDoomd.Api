using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Commands.StartBulkCreateAiUsers;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class StartBulkCreateAiUsersCommandHandlerTests
{
    private readonly Mock<IPersonaGenerator> _generator = new();
    private readonly Mock<IBulkCreateJobQueue> _queue = new();
    private readonly Mock<IBulkCreateJobStore> _store = new();

    private StartBulkCreateAiUsersCommandHandler CreateHandler() =>
        new(_generator.Object, _queue.Object, _store.Object);

    [Fact]
    public async Task Handle_WhenGeneratorNotConfigured_ReturnsFailure()
    {
        _generator.Setup(g => g.IsConfigured).Returns(false);

        var result = await CreateHandler().Handle(new StartBulkCreateAiUsersCommand(10), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Failure);
        result.Error!.Code.ShouldBe("persona.generator_unconfigured");
        _store.Verify(s => s.Create(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _queue.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenConfigured_ReturnsJobId_AndCreatesStoreEntry()
    {
        _generator.Setup(g => g.IsConfigured).Returns(true);
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var result = await CreateHandler().Handle(new StartBulkCreateAiUsersCommand(5), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        _store.Verify(s => s.Create(result.Value, 5), Times.Once);
        _queue.Verify(q => q.EnqueueAsync(result.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenCountIsZero_ShouldFail()
    {
        var validator = new StartBulkCreateAiUsersCommandValidator();
        var result = validator.Validate(new StartBulkCreateAiUsersCommand(0));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(StartBulkCreateAiUsersCommand.Count));
    }

    [Fact]
    public void Validator_WhenCountIs51_ShouldFail()
    {
        var validator = new StartBulkCreateAiUsersCommandValidator();
        var result = validator.Validate(new StartBulkCreateAiUsersCommand(51));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(StartBulkCreateAiUsersCommand.Count));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(50)]
    public void Validator_WhenCountIsValid_ShouldPass(int count)
    {
        var validator = new StartBulkCreateAiUsersCommandValidator();
        var result = validator.Validate(new StartBulkCreateAiUsersCommand(count));

        result.IsValid.ShouldBeTrue();
    }
}
