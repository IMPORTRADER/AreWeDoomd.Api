using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Common.Services;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.Common;

public sealed class AiAccountFactoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();

    private AiAccountFactory CreateFactory() => new(_users.Object, _hasher.Object);

    [Fact]
    public async Task CreateAiAccountAsync_WhenUsernameTaken_ReturnsConflict_AddNeverCalled()
    {
        _users.Setup(r => r.IsUsernameTakenAsync("botty", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateFactory().CreateAiAccountAsync("botty", "botty@test.com", "pw123456", Now, default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("auth.username_taken");
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAiAccountAsync_WhenEmailTaken_ReturnsConflict_AddNeverCalled()
    {
        _users.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailTakenAsync("botty@test.com", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateFactory().CreateAiAccountAsync("botty", "botty@test.com", "pw123456", Now, default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.Conflict);
        result.Error!.Code.ShouldBe("auth.email_taken");
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAiAccountAsync_WhenSuccess_AddsAiUser_WithHashedPassword()
    {
        _users.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("raw-password")).Returns("hashed-password-1234567890");

        var result = await CreateFactory().CreateAiAccountAsync("botty", "botty@test.com", "raw-password", Now, default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.UserType.ShouldBe(UserType.Ai);
        result.Value.PasswordHash.ShouldBe("hashed-password-1234567890");
        _hasher.Verify(h => h.Hash("raw-password"), Times.Once);
        _users.Verify(r => r.AddAsync(result.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAiAccountAsync_WhenSuccess_NeverCallsSaveChanges()
    {
        // The factory has no IUnitOfWork dependency — SaveChanges is structurally impossible.
        // Only IUserRepository and IPasswordHasher are injected; a successful call proves no save was made.
        _users.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailTakenAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password-1234567890");

        var result = await CreateFactory().CreateAiAccountAsync("botty", "botty@test.com", "raw-password", Now, default);

        result.IsSuccess.ShouldBeTrue();
        // Verify only AddAsync was called on the repository — no SaveChanges exists on IUserRepository.
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
