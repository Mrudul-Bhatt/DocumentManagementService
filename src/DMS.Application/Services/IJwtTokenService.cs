using DMS.Domain.Enums;

namespace DMS.Application.Services;

/// <summary>
/// Defines the contract for generating JWT access tokens and opaque refresh tokens.
///
/// Why in Application and not Domain?
///   Token generation requires the JWT library (System.IdentityModel.Tokens.Jwt) and
///   configuration (secret, issuer, expiry). These are infrastructure concerns. The
///   interface lives in Application so handlers can use it without referencing
///   Infrastructure; the concrete JwtTokenService in Infrastructure does the actual work.
///
/// Why two separate token types?
///   Access token — a signed JWT carrying identity claims (userId, email, role).
///     Short-lived (default 60 min). Verified stateless by the JWT Bearer middleware on
///     every request — no database lookup required. Stored in memory on the client.
///   Refresh token — a cryptographically random opaque string stored in the database.
///     Long-lived (default 7 days). Used only to obtain a new access token via
///     POST /auth/refresh. Stored in localStorage on the client.
///
///   This split limits damage from compromise: a stolen access token is only valid for
///   its short lifetime. A stolen refresh token is detectable via refresh token rotation
///   (when the legitimate user's next refresh fails, they know the token was used).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT access token for the given user.
    ///
    /// Claims embedded in the token:
    ///   sub  (NameIdentifier) — userId, read by ClaimsPrincipalExtensions.GetUserId()
    ///   email                 — user's email address
    ///   role                  — user's Role enum value (User / Admin)
    ///   jti                   — unique token ID (enables per-token revocation if needed)
    ///
    /// The token is signed with HMAC-SHA256 using the secret from JwtSettings.
    /// Expiry is set to DateTimeOffset.UtcNow + JwtSettings.ExpiryMinutes.
    /// </summary>
    string GenerateAccessToken(Guid userId, string email, Role role);

    /// <summary>
    /// Generates a cryptographically random opaque refresh token string.
    ///
    /// Why opaque (random bytes) instead of a JWT?
    ///   A JWT refresh token could be validated without a database lookup, but it
    ///   cannot be individually revoked without a denylist. An opaque token stored in
    ///   the database is trivially revoked by deleting or marking the row — enabling
    ///   refresh token rotation and logout-everywhere functionality.
    ///
    /// Implementation: 64 random bytes from RandomNumberGenerator → Base64 string.
    /// The resulting string is stored in RefreshToken.Token and sent to the client.
    /// </summary>
    string GenerateRefreshToken();
}
