using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Commands.ChangeEmail;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Features.Users.Commands.ChangeEmail;

public sealed class ChangeEmailCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IAccessTokenGenerator> _accessTokenGeneratorMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ChangeEmailCommandHandler _handler;

    public ChangeEmailCommandHandlerTests()
    {
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(Now);
        _handler = new ChangeEmailCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _accessTokenGeneratorMock.Object,
            _dateTimeProviderMock.Object,
            _unitOfWorkMock.Object);
    }

    private static User CreateUser() =>
        new(Guid.NewGuid(), "testuser", "old@example.com", "hashedpassword_at_least_20_chars", UserType.Human, Now);

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        _userRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(
            new ChangeEmailCommand(Guid.NewGuid(), "pw", "new@example.com"), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("user.not_found");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsFailure()
    {
        var user = CreateUser();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify(user.PasswordHash, "wrong")).Returns(false);

        var result = await _handler.Handle(
            new ChangeEmailCommand(user.Id, "wrong", "new@example.com"), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.Failure);
        result.Error!.Code.ShouldBe("user.invalid_password");
    }

    [Fact]
    public async Task Handle_EmailTaken_ReturnsConflict()
    {
        var user = CreateUser();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify(user.PasswordHash, "pw")).Returns(true);
        _userRepositoryMock.Setup(r => r.IsEmailTakenAsync("taken@example.com", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(
            new ChangeEmailCommand(user.Id, "pw", "taken@example.com"), CancellationToken.None);

        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("user.email_taken");
    }

    [Fact]
    public async Task Handle_ValidRequest_NormalizesEmailAndReturnsToken()
    {
        var user = CreateUser();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.Verify(user.PasswordHash, "pw")).Returns(true);
        _userRepositoryMock.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _accessTokenGeneratorMock.Setup(t => t.Generate(It.IsAny<User>())).Returns("new-jwt");

        var result = await _handler.Handle(
            new ChangeEmailCommand(user.Id, "pw", "New@Example.com"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AccessToken.ShouldBe("new-jwt");
        user.Email.ShouldBe("new@example.com");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
