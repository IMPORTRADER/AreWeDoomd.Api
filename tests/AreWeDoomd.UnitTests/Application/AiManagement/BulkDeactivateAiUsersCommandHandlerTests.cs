using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Commands.BulkDeactivateAiUsers;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public class BulkDeactivateAiUsersCommandHandlerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private BulkDeactivateAiUsersCommandHandler CreateHandler()
    {
        _dateTimeProvider.SetupGet(d => d.UtcNow).Returns(DateTimeOffset.UtcNow);
        return new BulkDeactivateAiUsersCommandHandler(
            _repository.Object, _dateTimeProvider.Object, _unitOfWork.Object);
    }

    private static User CreateAiUser() =>
        User.Create($"ai{Guid.NewGuid():N}"[..10], "a@a.com", new string('x', 30), UserType.Ai, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_WhenAllUsersAreAi_ShouldDeactivateAllAndReturnCount()
    {
        var users = new List<User> { CreateAiUser(), CreateAiUser() };
        _repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        var command = new BulkDeactivateAiUsersCommand(users.Select(u => u.Id).ToList(), Deactivate: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        users.ShouldAllBe(u => u.IsDeactivated);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAnyUserIsHuman_ShouldReturnFailure()
    {
        var aiUser = CreateAiUser();
        var humanUser = User.Create("humanuser1", "h@h.com", new string('x', 30), UserType.Human, DateTimeOffset.UtcNow);
        var users = new List<User> { aiUser, humanUser };
        _repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        var command = new BulkDeactivateAiUsersCommand(users.Select(u => u.Id).ToList(), Deactivate: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("BulkDeactivate.NonAiUser");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnyIdMissing_ShouldReturnNotFound()
    {
        var aiUser = CreateAiUser();
        var missingId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { aiUser });
        var command = new BulkDeactivateAiUsersCommand(
            new List<Guid> { aiUser.Id, missingId }, Deactivate: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("BulkDeactivate.UserNotFound");
    }

    [Fact]
    public async Task Handle_WhenDeactivateFalse_ShouldReactivateUsers()
    {
        var now = DateTimeOffset.UtcNow;
        var aiUser = CreateAiUser();
        aiUser.Deactivate(now);
        aiUser.IsDeactivated.ShouldBeTrue();
        var users = new List<User> { aiUser };
        _repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        var command = new BulkDeactivateAiUsersCommand(
            new List<Guid> { aiUser.Id }, Deactivate: false);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        aiUser.IsDeactivated.ShouldBeFalse();
    }
}
