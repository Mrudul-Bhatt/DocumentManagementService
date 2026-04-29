using DMS.Api.Extensions;
using DMS.Application.Auth.Commands.Login;
using Microsoft.AspNetCore.RateLimiting;
using DMS.Application.Auth.Commands.RefreshToken;
using DMS.Application.Auth.Commands.Register;
using DMS.Application.Auth.Commands.RevokeToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RegisterCommand(request.Email, request.Password), ct);
        return result.IsFailure ? result.ToProblemResult(this) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new LoginCommand(request.Email, request.Password, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] TokenRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RefreshTokenCommand(request.Token), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke([FromBody] TokenRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RevokeTokenCommand(request.Token), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }
}

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record TokenRequest(string Token);
