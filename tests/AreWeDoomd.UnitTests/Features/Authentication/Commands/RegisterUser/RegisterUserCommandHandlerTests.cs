using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Authentication.Commands.RegisterUser;

public sealed class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IAccessTokenGenerator> _accessTokenGeneratorMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RegisterUserCommandHandler _handler;

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public RegisterUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _accessTokenGeneratorMock = new Mock<IAccessTokenGenerator>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(Now);
        _passwordHasherMock.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashedpassword_at_least_20_chars");
        _accessTokenGeneratorMock.Setup(a => a.Generate(It.IsAny<User>())).Returns("access-token");

        _handler = new RegisterUserCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _accessTokenGeneratorMock.Object,
            _dateTimeProviderMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUsernameIsAlreadyTaken_ShouldReturnConflict()
    {
        var command = new RegisterUserCommand("takenuser", "new@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("takenuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("auth.username_taken");
    }

    [Fact]
    public async Task Handle_WhenEmailIsAlreadyTaken_ShouldReturnConflict()
    {
        var command = new RegisterUserCommand("newuser", "taken@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("newuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync("taken@example.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("auth.email_taken");
    }

    [Fact]
    public async Task Handle_WhenUsernameAndEmailAreAvailable_ShouldReturnSuccess()
    {
        var command = new RegisterUserCommand("newuser", "new@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("newuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync("new@example.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Username.ShouldBe("newuser");
        result.Value.Email.ShouldBe("new@example.com");
        result.Value.AccessToken.ShouldBe("access-token");
    }

    [Fact]
    public async Task Handle_WhenUsernameAndEmailAreAvailable_ShouldPersistUser()
    {
        var command = new RegisterUserCommand("newuser", "new@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("newuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync("new@example.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _handler.Handle(command, CancellationToken.None);

        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUsernameIsTaken_ShouldNotCheckEmail()
    {
        var command = new RegisterUserCommand("takenuser", "new@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("takenuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _handler.Handle(command, CancellationToken.None);

        _userRepositoryMock.Verify(
            r => r.IsEmailTakenAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUsernameIsTaken_ShouldNotPersistUser()
    {
        var command = new RegisterUserCommand("takenuser", "new@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync("takenuser", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _handler.Handle(command, CancellationToken.None);

        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRegistering_ShouldPassNullAsExcludeUserIdToUniquenessChecks()
    {
        var command = new RegisterUserCommand("newuser", "new@example.com", "password123", UserType.Human);

        _userRepositoryMock
            .Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _handler.Handle(command, CancellationToken.None);

        _userRepositoryMock.Verify(
            r => r.IsUsernameTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            r => r.IsEmailTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
