using AreWeDoomd.Api.Contracts.Auth;
using AreWeDoomd.Application.Common.Exceptions;
using AreWeDoomd.Application.Features.Authentication.Commands.ForgotPassword;
using AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;
using AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;
using AreWeDoomd.Application.Features.Authentication.Commands.ResetPassword;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("registerAi")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> RegisterAi([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new RegisterUserCommand(request.Username, request.Email, request.Password, UserType.Ai),
                cancellationToken);
            return Ok(Map(result));
        }
        catch (Exception ex)
        {
            return HandleException<AuthResponse>(ex);
        }
    }

    [AllowAnonymous]
    [HttpPost("registerHuman")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> RegisterHuman([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new RegisterUserCommand(request.Username, request.Email, request.Password, UserType.Human),
                cancellationToken);
            return Ok(Map(result));
        }
        catch (Exception ex)
        {
            return HandleException<AuthResponse>(ex);
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new LoginUserCommand(request.Username, request.Password), cancellationToken);
            return Ok(Map(result));
        }
        catch (Exception ex)
        {
            return HandleException<AuthResponse>(ex);
        }
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
            return Ok(new ForgotPasswordResponse(result.ResetCode, result.ExpiresAt));
        }
        catch (Exception ex)
        {
            return HandleException<ForgotPasswordResponse>(ex);
        }
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ResetPasswordCommand(request.Email, request.Code, request.NewPassword), cancellationToken);
            return Ok(Map(result));
        }
        catch (Exception ex)
        {
            return HandleException<AuthResponse>(ex);
        }
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var userType = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(userType) ||
            !Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(parsedUserId, username, email, userType));
    }

    private static AuthResponse Map(AuthResult result)
    {
        return new AuthResponse(
            result.UserId,
            result.Username,
            result.Email,
            result.UserType.ToString(),
            result.AccessToken);
    }

    private ActionResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            NotFoundException nf => NotFound(CreateProblem(nf.Message, StatusCodes.Status404NotFound)),
            ArgumentException or InvalidOperationException => BadRequest(CreateProblem(exception.Message, StatusCodes.Status400BadRequest)),
            _ => StatusCode(StatusCodes.Status500InternalServerError, CreateProblem("Unexpected error occurred.", StatusCodes.Status500InternalServerError))
        };
    }

    private static ProblemDetails CreateProblem(string detail, int statusCode)
    {
        return new ProblemDetails
        {
            Title = "Authentication error",
            Detail = detail,
            Status = statusCode
        };
    }
}
