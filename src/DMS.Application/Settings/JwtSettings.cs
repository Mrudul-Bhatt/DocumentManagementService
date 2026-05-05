namespace DMS.Application.Settings;

/// <summary>
/// Strongly-typed settings for JWT token generation and validation.
/// Bound to the "Jwt" section of appsettings.json via the Options pattern.
///
/// Why the Options pattern (IOptions&lt;JwtSettings&gt;) instead of IConfiguration?
///   IConfiguration is a key-value string store — accessing values requires
///   magic strings ("Jwt:Secret") that aren't type-checked and can silently be
///   wrong after a rename. A strongly-typed class surfaces missing or mismatched
///   settings at startup rather than at runtime. It also keeps IConfiguration
///   out of the Application layer, which should not depend on configuration
///   infrastructure directly (that dependency belongs in Infrastructure).
///
/// Why init-only setters?
///   Settings are read once at startup and never mutated. init prevents accidental
///   modification after binding. The class is sealed for the same reason as other
///   leaf types — no subclassing intended.
///
/// Registration in DMS.Infrastructure.DependencyInjection:
///   services.Configure&lt;JwtSettings&gt;(configuration.GetSection("Jwt"))
///   Handlers receive it via constructor injection as IOptions&lt;JwtSettings&gt;
///   and read the value with jwtOptions.Value.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>
    /// HMAC-SHA256 signing secret. Must be at least 32 characters (256 bits) long.
    /// SECURITY: Never commit a real secret to source control. Use environment
    /// variables or a secrets manager in non-development environments.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <summary>
    /// Token issuer claim ("iss"). Validated by the JWT Bearer middleware.
    /// Typically the service name or URL (e.g., "DMS").
    /// </summary>
    public string Issuer { get; init; } = default!;

    /// <summary>
    /// Token audience claim ("aud"). Validated by the JWT Bearer middleware.
    /// Identifies the intended recipients of the token (e.g., "DMS").
    /// </summary>
    public string Audience { get; init; } = default!;

    /// <summary>
    /// Lifetime of the JWT access token in minutes. Default: 60.
    /// Keep short — access tokens are stateless and cannot be individually revoked
    /// until they expire. A shorter window limits damage from a stolen token.
    /// </summary>
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>
    /// Lifetime of the refresh token in days. Default: 7.
    /// Refresh tokens are stored in the database and can be revoked at any time.
    /// Longer lifetime improves UX (fewer re-logins) at the cost of a wider
    /// revocation window if a refresh token is compromised.
    /// </summary>
    public int RefreshTokenExpiryDays { get; init; } = 7;
}
