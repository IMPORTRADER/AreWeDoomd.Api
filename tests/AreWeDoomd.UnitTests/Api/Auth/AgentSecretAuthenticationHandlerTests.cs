using System.Security.Claims;
using System.Text.Encodings.Web;
using AreWeDoomd.Api.Auth;
using AreWeDoomd.Api.Realtime.Options;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Api.Auth;

public sealed class AgentSecretAuthenticationHandlerTests
{
    private const string Secret = "test-secret";
    private static readonly Guid AiUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task HandleAuthenticateAsync_WhenValidSecretAndAiUser_ShouldSucceedWithClaims()
    {
        var user = AiUser();
        var result = await RunAsync(secret: Secret, userId: AiUserId.ToString(), user);

        result.Succeeded.ShouldBeTrue();
        var principal = result.Principal!;
        principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(AiUserId.ToString());
        principal.FindFirstValue(ClaimTypes.Name).ShouldBe("doombot");
        principal.FindFirstValue(ClaimTypes.Role).ShouldBe(nameof(UserType.Ai));
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WhenUserIdHeaderMissing_ShouldReturnNoResult()
    {
        var result = await RunAsync(secret: Secret, userId: null, AiUser());

        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WhenSecretWrong_ShouldFail()
    {
        var result = await RunAsync(secret: "wrong", userId: AiUserId.ToString(), AiUser());

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WhenUserIsHuman_ShouldFail()
    {
        var human = new User(
            AiUserId, "alice", "alice@test.local",
            new string('x', 24), UserType.Human, DateTimeOffset.UtcNow);

        var result = await RunAsync(secret: Secret, userId: AiUserId.ToString(), human);

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WhenUserNotFound_ShouldFail()
    {
        var result = await RunAsync(secret: Secret, userId: AiUserId.ToString(), user: null);

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    private static User AiUser()
    {
        return new User(
            AiUserId, "doombot", "doombot@test.local",
            new string('x', 24), UserType.Ai, DateTimeOffset.UtcNow);
    }

    private static async Task<AuthenticateResult> RunAsync(string? secret, string? userId, User? user)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor
            .Setup(m => m.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions());

        var handler = new AgentSecretAuthenticationHandler(
            optionsMonitor.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new AgentNotificationsOptions { SharedSecret = Secret }),
            repository.Object);

        var context = new DefaultHttpContext();
        if (secret is not null)
        {
            context.Request.Headers["X-Agent-Secret"] = secret;
        }

        if (userId is not null)
        {
            context.Request.Headers["X-Agent-User-Id"] = userId;
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(
                AgentSecretAuthenticationDefaults.SchemeName,
                displayName: null,
                typeof(AgentSecretAuthenticationHandler)),
            context);

        return await handler.AuthenticateAsync();
    }
}
