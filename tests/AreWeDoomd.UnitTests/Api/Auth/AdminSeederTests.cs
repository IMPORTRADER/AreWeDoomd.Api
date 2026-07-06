using AreWeDoomd.Api.Auth;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Api.Auth;

public sealed class AdminSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string ValidHash = "this-is-a-valid-hash-at-least-20-chars";

    [Fact]
    public async Task SeedCoreAsync_WhenUserFoundAndNotAdmin_ShouldGrantUpdateAndSaveOnce()
    {
        var user = User.Create("adminuser", "admin@test.local", ValidHash, UserType.Human, Now);
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepo
            .Setup(r => r.GetByUsernameAsync("adminuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await AdminSeeder.SeedCoreAsync(
            ["adminuser"],
            userRepo.Object,
            unitOfWork.Object,
            Now,
            NullLogger.Instance,
            CancellationToken.None);

        result.ShouldBe(1);
        user.IsAdmin.ShouldBeTrue();
        userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SeedCoreAsync_WhenUserNotFound_ShouldSkipWithoutThrowingAndNotSave()
    {
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepo
            .Setup(r => r.GetByUsernameAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await AdminSeeder.SeedCoreAsync(
            ["missing"],
            userRepo.Object,
            unitOfWork.Object,
            Now,
            NullLogger.Instance,
            CancellationToken.None);

        result.ShouldBe(0);
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeedCoreAsync_WhenUserAlreadyAdmin_ShouldSkipUpdateAndSave()
    {
        var user = User.Create("adminuser", "admin@test.local", ValidHash, UserType.Human, Now);
        user.GrantAdmin(Now);

        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        userRepo
            .Setup(r => r.GetByUsernameAsync("adminuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await AdminSeeder.SeedCoreAsync(
            ["adminuser"],
            userRepo.Object,
            unitOfWork.Object,
            Now,
            NullLogger.Instance,
            CancellationToken.None);

        result.ShouldBe(0);
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeedCoreAsync_WhenListContainsOnlyBlankOrNullEntries_ShouldReturnZeroWithoutCallingRepository()
    {
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var result = await AdminSeeder.SeedCoreAsync(
            new List<string> { "", "  ", null! },
            userRepo.Object,
            unitOfWork.Object,
            Now,
            NullLogger.Instance,
            CancellationToken.None);

        result.ShouldBe(0);
        userRepo.Verify(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeedCoreAsync_WhenListIsEmpty_ShouldNotCallRepositoryAndReturnZero()
    {
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var result = await AdminSeeder.SeedCoreAsync(
            [],
            userRepo.Object,
            unitOfWork.Object,
            Now,
            NullLogger.Instance,
            CancellationToken.None);

        result.ShouldBe(0);
        userRepo.Verify(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
