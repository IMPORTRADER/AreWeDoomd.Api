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

namespace AreWeDoomd.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
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

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new LoginUserCommand(request.Email, request.Password), cancellationToken);
            return Ok(Map(result));
        }
        catch (Exception ex)
        {
            return HandleException<AuthResponse>(ex);
        }
    }

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

    private static AuthResponse Map(AuthResult result)
    {
        return new AuthResponse(
            result.UserId,
            result.Username,
            result.Email,
            result.UserType.ToString());
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

