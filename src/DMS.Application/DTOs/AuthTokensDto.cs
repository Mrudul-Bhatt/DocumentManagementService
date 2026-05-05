namespace DMS.Application.DTOs;

/// <summary>
/// Response payload returned to the client on successful login or token refresh.
///
/// Why return both tokens in one DTO?
///   The client needs both simultaneously after authentication. A single response
///   eliminates the need for a follow-up request to obtain the refresh token.
///
/// Why include ExpiresInSeconds?
///   Without knowing when the access token expires, the client must either parse
///   the JWT to read the "exp" claim (adds complexity) or refresh proactively on
///   every 401 response (adds latency). Providing the expiry upfront lets the
///   client schedule a silent refresh just before expiry, keeping the user session
///   seamless. Value is derived from JwtSettings.ExpiryMinutes * 60.
///
/// Client storage convention:
///   AccessToken  — stored in memory only (never localStorage / sessionStorage).
///                  Lost on page refresh; restored via RefreshToken on next load.
///                  In-memory storage prevents XSS scripts from stealing the token.
///   RefreshToken — stored in localStorage so it survives page refreshes.
///                  Longer-lived but can be revoked server-side at any time.
/// </summary>
public sealed record AuthTokensDto(
    /// <summary>Signed JWT access token. Short-lived. Set as Authorization: Bearer header on API requests.</summary>
    string AccessToken,

    /// <summary>Opaque refresh token. Long-lived. Used only on POST /auth/refresh.</summary>
    string RefreshToken,

    /// <summary>Number of seconds until the access token expires. Used by the client to schedule silent refresh.</summary>
    int ExpiresInSeconds);
