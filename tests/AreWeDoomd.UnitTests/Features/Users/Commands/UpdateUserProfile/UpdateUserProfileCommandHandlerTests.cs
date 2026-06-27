using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Commands.UpdateUserProfile;
using AreWeDoomd.Application.Features.Users.Common;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Users.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IProfileStatsRepository> _profileStatsRepositoryMock;
    private readonly UpdateUserProfileCommandHandler _handler;

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public UpdateUserProfileCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _profileStatsRepositoryMock = new Mock<IProfileStatsRepository>();

        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(Now);
        _profileStatsRepositoryMock
            .Setup(s => s.GetStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileStatsResult(0, 0, 0, 0, 0));

        _handler = new UpdateUserProfileCommandHandler(
            _userRepositoryMock.Object,
            _dateTimeProviderMock.Object,
            _unitOfWorkMock.Object,
            _profileStatsRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnNotFound()
    {
        var command = new UpdateUserProfileCommand(Guid.NewGuid(), "newname", null, null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("user.not_found");
    }

    [Fact]
    public async Task Handle_WhenUsernameIsSameAsCurrentUser_ShouldReturnSuccess()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, user.Username, null, null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync(user.Username, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenUsernameIsTakenByAnotherUser_ShouldReturnConflict()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, "takenname", null, null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("takenname", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("user.username_taken");
    }

    [Fact]
    public async Task Handle_WhenEmailIsSameAsCurrentUser_ShouldReturnSuccess()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, null, user.Email, null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync(user.Email, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenEmailIsTakenByAnotherUser_ShouldReturnConflict()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, null, "taken@example.com", null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync("taken@example.com", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("user.email_taken");
    }

    [Fact]
    public async Task Handle_WhenUsernameAndEmailAreAvailable_ShouldReturnSuccess()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, "newname", "new@example.com", null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("newname", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync("new@example.com", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Username.ShouldBe("newname");
        result.Value.IsMe.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenBioChanges_ShouldReturnRichProfileWithStats()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, null, null, "Allegedly human.");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _profileStatsRepositoryMock
            .Setup(s => s.GetStatsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileStatsResult(5, 4, 3, 2, 1));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsMe.ShouldBeTrue();
        result.Value.Bio.ShouldBe("Allegedly human.");
        result.Value.Stats.PostCount.ShouldBe(5);
    }

    [Fact]
    public async Task Handle_WhenUsernameIsTakenByAnotherUser_ShouldNotCallIsEmailTaken()
    {
        var user = CreateUser();
        var command = new UpdateUserProfileCommand(user.Id, "takenname", "new@example.com", null);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("takenname", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _handler.Handle(command, CancellationToken.None);

        _userRepositoryMock.Verify(
            r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static User CreateUser()
    {
        return new User(
            Guid.NewGuid(),
            "testuser",
            "test@example.com",
            "hashedpassword_at_least_20_chars",
            UserType.Human,
            Now);
    }
}
