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

/// <summary>
/// Handles all HTTP operations for authentication: register, login, token refresh, and revoke.
///
/// Design contract:
///   Like FilesController, this controller contains zero business logic. Every action
///   constructs a Command, dispatches it via MediatR, and maps the Result to an HTTP
///   response. All decisions about password hashing, token issuance, credential validation,
///   and account status live in DMS.Application and DMS.Domain.
///
/// Token strategy (Level 1):
///   Authentication uses a dual-token model:
///     Access token  — short-lived JWT (default 60 min), stored in memory on the client.
///                     Sent in the Authorization: Bearer header on every API request.
///     Refresh token — long-lived opaque token (default 7 days), stored in the client's
///                     localStorage. Used only on POST /auth/refresh to obtain a new
///                     access token without re-entering credentials.
///   Keeping the access token in memory (not localStorage) limits XSS exposure:
///   a script injection cannot steal the access token across page reloads.
///
/// Why no [Authorize] on the class?
///   Register, Login, and Refresh must be publicly accessible — they are the endpoints
///   callers use to obtain credentials in the first place. Only Revoke requires an
///   authenticated caller and carries [Authorize] at the method level.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Creates a new user account.
    ///
    /// Flow:
    ///   1. Dispatch RegisterCommand with email and password.
    ///   2. Handler validates uniqueness, hashes the password (BCrypt, cost 12), persists.
    ///   3. On success, return 201 Created with the new user's public metadata.
    ///
    /// Why 201 Created and not 200 OK?
    ///   A new resource (the user account) has been created. REST convention is 201.
    ///   The response body contains the created resource's data so the client can
    ///   immediately display or use it without a separate GET request.
    ///
    /// Why 409 Conflict for duplicate email?
    ///   The request is well-formed but conflicts with existing state (a registered account
    ///   with the same email). 409 is more precise than 400 and tells the client exactly
    ///   what went wrong so it can show "this email is already registered".
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RegisterCommand(request.Email, request.Password), ct);
        return result.IsFailure ? result.ToProblemResult(this) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Validates credentials and issues a JWT access token + refresh token pair.
    ///
    /// Flow:
    ///   1. Extract the caller's IP address for audit logging.
    ///   2. Dispatch LoginCommand — handler checks email exists, verifies BCrypt hash,
    ///      confirms account is active, then issues both tokens.
    ///   3. On success, return 200 OK with the token pair in the response body.
    ///
    /// Why [EnableRateLimiting("login")]?
    ///   The login endpoint is the primary target for credential stuffing and brute-force
    ///   attacks. The "login" sliding window policy (configured in Program.cs) caps
    ///   attempts at 5 per minute per IP. Exceeding the limit returns 429 Too Many Requests.
    ///   Rate limiting is applied at the endpoint level (not globally) so other endpoints
    ///   are unaffected.
    ///
    /// Why capture IP here?
    ///   The IP is passed to LoginCommand which implements IAuditableRequest. The
    ///   AuditLoggingBehaviour pipeline captures it and writes an AuditLog record
    ///   so every successful login is traceable to a specific IP address.
    ///
    /// Why deliberately vague on failure?
    ///   The handler returns User.InvalidCredentials for both "email not found" and
    ///   "wrong password". This prevents user enumeration — an attacker cannot tell
    ///   whether an account exists by observing different error messages.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // IP is nullable — RemoteIpAddress is null when the connection comes through
        // certain reverse proxies or when running in test environments. The handler
        // and audit log accept null gracefully.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new LoginCommand(request.Email, request.Password, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Issues a new access token + refresh token pair using a valid, unexpired refresh token.
    ///
    /// Flow:
    ///   1. Dispatch RefreshTokenCommand with the caller's refresh token string.
    ///   2. Handler looks up the token, verifies it is active (not revoked, not expired),
    ///      revokes the old token, and issues a fresh pair (refresh token rotation).
    ///   3. On success, return 200 OK with the new token pair.
    ///
    /// Why no [Authorize] on this endpoint?
    ///   The caller's access token may be expired — that is precisely the situation where
    ///   they need to call this endpoint. Requiring a valid access token would make it
    ///   impossible to refresh, defeating the purpose of refresh tokens entirely.
    ///   The refresh token itself is the credential that authorises this operation.
    ///
    /// Why refresh token rotation?
    ///   Each call to this endpoint revokes the presented refresh token and issues a new
    ///   one. If a refresh token is stolen and used, the legitimate owner's next refresh
    ///   attempt fails (their token was revoked by the attacker's use), alerting them to
    ///   the compromise. Without rotation, a stolen token is valid for its full lifetime.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] TokenRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RefreshTokenCommand(request.Token), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Revokes a refresh token, preventing it from being used for future token refreshes.
    ///
    /// Flow:
    ///   1. [Authorize] ensures only an authenticated caller can reach this action.
    ///   2. Dispatch RevokeTokenCommand with the token string to revoke.
    ///   3. Handler marks the token as revoked in the database.
    ///   4. On success, return 204 No Content.
    ///
    /// Why [Authorize] here but not on Refresh?
    ///   Revoke is a deliberate account action (logout). The caller must prove they hold
    ///   a valid access token — revoking a token on behalf of an unauthenticated request
    ///   would allow anyone to invalidate arbitrary tokens by guessing token strings.
    ///
    /// Why 204 No Content on success?
    ///   The token has been invalidated — there is no meaningful resource to return.
    ///   REST convention for a successful destructive operation with no response body.
    /// </summary>
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

// ---------------------------------------------------------------------------
// Request record types
//
// Why inline record types instead of separate files?
//   These records are tiny (1-2 properties), used only by this controller, and
//   have no behaviour. Keeping them here avoids creating three files for three
//   one-liners. If a request type grows (validation attributes, nested objects),
//   it warrants its own file at that point.
//
// Why sealed records?
//   Request types are not designed for inheritance. sealed prevents accidental
//   subclassing, and records give value equality and a compact constructor for free.
// ---------------------------------------------------------------------------

/// <summary>Payload for POST /auth/register.</summary>
public sealed record RegisterRequest(string Email, string Password);

/// <summary>Payload for POST /auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Payload for POST /auth/refresh and POST /auth/revoke.</summary>
public sealed record TokenRequest(string Token);
