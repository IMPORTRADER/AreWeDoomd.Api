using System.Security.Claims;
using System.Text.Encodings.Web;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Realtime;
using AreWeDoomd.Api.Realtime.Options;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Api.Auth;

public sealed class AgentSecretAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<AgentNotificationsOptions> agentOptions,
    IUserRepository userRepository)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AgentImpersonationConstants.UserIdHeaderName, out var userIdRaw))
        {
            return AuthenticateResult.NoResult();
        }

        string providedSecret = Request.Headers[AgentNotificationHubConstants.SecretHeaderName].ToString();
        if (!AgentSecretValidator.IsValid(providedSecret, agentOptions.Value.SharedSecret))
        {
            return AuthenticateResult.Fail("Invalid agent secret.");
        }

        if (!Guid.TryParse(userIdRaw.ToString(), out var userId))
        {
            return AuthenticateResult.Fail("Invalid agent user id.");
        }

        var user = await userRepository.GetByIdAsync(userId, Context.RequestAborted);
        if (user is null || user.UserType != UserType.Ai)
        {
            return AuthenticateResult.Fail("Agent user not found or is not an AI user.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, nameof(UserType.Ai))
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
