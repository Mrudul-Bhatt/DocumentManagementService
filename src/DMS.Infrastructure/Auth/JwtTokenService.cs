using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DMS.Application.Services;
using DMS.Application.Settings;
using DMS.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DMS.Infrastructure.Auth;

/// <summary>
/// JWT and refresh token generation service.
///
/// Why internal sealed?
///   Infrastructure detail — consumers depend on IJwtTokenService from the Application
///   layer. No code outside DMS.Infrastructure should reference this class directly.
///
/// Why Singleton lifetime (registered in DependencyInjection)?
///   This class holds no per-request state: _jwt is read once from IOptions at
///   construction and never mutated. All methods are pure transforms that produce a new
///   token string on each call. Singleton allows the JwtSettings object to be read once
///   at startup rather than re-evaluated on every request.
///
/// Why IOptions<JwtSettings> instead of IConfiguration?
///   The Options pattern provides strongly typed access to configuration and validates
///   required fields at startup (if using IOptions with validation). More importantly,
///   IConfiguration is an Infrastructure concern — reading raw config strings directly
///   would couple this class to the configuration system. JwtSettings is defined in the
///   Application layer and represents the contract for what configuration this service needs.
/// </summary>
internal sealed class JwtTokenService(IOptions<JwtSettings> jwtOptions) : IJwtTokenService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Generates a signed JWT access token embedding the user's identity and role.
    ///
    /// Claims embedded in the token:
    ///   - Sub (subject): the user's GUID as a string. This is the standard JWT field for
    ///     the principal's identity. ASP.NET Core maps it to ClaimTypes.NameIdentifier,
    ///     which ClaimsPrincipalExtensions.GetUserId() reads to authenticate requests.
    ///   - Email: the user's canonical (lower-cased) email address. Included for convenience
    ///     — clients can read the email from the token without a separate profile endpoint.
    ///   - Role: the user's Role enum value as a string ("User" or "Admin"). ASP.NET Core's
    ///     [Authorize(Roles = "Admin")] reads this claim. ClaimTypes.Role is used (not
    ///     JwtRegisteredClaimNames) because ASP.NET Core's role-based authorization checks
    ///     the ClaimTypes.Role claim type specifically.
    ///   - Jti (JWT ID): a unique GUID per token. Provides a unique identifier for this
    ///     specific token instance, enabling future denylist-based revocation (a future
    ///     level feature). Without Jti, two tokens issued to the same user at the same
    ///     second would be identical strings — Jti guarantees uniqueness.
    ///
    /// Why HMAC-SHA256 (symmetric) instead of RSA (asymmetric)?
    ///   Symmetric signing uses the same secret for both signing and verification —
    ///   simpler to configure, no certificate management. Asymmetric (RSA) is needed when
    ///   third parties need to verify tokens without possessing the signing key (e.g., a
    ///   separate microservice). Since this system's single API both issues and validates
    ///   tokens, symmetric is sufficient and simpler.
    ///
    /// Why DateTime.UtcNow (not DateTimeOffset.UtcNow) for expiry?
    ///   JwtSecurityToken's expires parameter accepts DateTime. The JWT spec represents
    ///   expiry as Unix epoch seconds, which are inherently UTC. Using UtcNow ensures the
    ///   expiry is always UTC regardless of the server's local timezone setting.
    /// </summary>
    public string GenerateAccessToken(Guid userId, string email, Role role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically secure opaque refresh token string.
    ///
    /// Why RandomNumberGenerator.GetBytes(64) instead of Guid.NewGuid()?
    ///   Guid.NewGuid() uses a pseudo-random algorithm that is not cryptographically secure —
    ///   its bits follow predictable patterns that reduce the effective entropy. A CSPRNG
    ///   (Cryptographically Secure Pseudo-Random Number Generator) like RandomNumberGenerator
    ///   provides full entropy over all bits. 64 bytes = 512 bits of entropy, far beyond
    ///   what any brute-force attack could enumerate.
    ///
    /// Why Convert.ToBase64String?
    ///   Base64 encodes arbitrary bytes into a URL-safe(ish) ASCII string suitable for
    ///   storage in the database and transmission in HTTP responses/cookies.
    ///   64 bytes of base64 produces an 88-character string (padded). The Token column's
    ///   MaxLength(512) in RefreshTokenConfiguration comfortably accommodates this.
    ///
    /// The returned string is stored in RefreshToken.Token (unique indexed column).
    /// </summary>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
