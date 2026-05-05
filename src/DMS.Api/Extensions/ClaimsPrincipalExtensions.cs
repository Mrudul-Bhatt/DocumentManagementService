using System.Security.Claims;

namespace DMS.Api.Extensions;

/// <summary>
/// Extension methods on ClaimsPrincipal for reading well-known JWT claims.
///
/// Why extension methods instead of reading claims directly in each controller?
///   Controllers that read HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
///   directly scatter the claim name constant and the null-check logic across every
///   action method. An extension method centralises that logic in one place — if the
///   claim name ever changes (e.g., moving from ClaimTypes.NameIdentifier to a custom
///   "sub" claim), one edit here fixes every caller.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated user's ID from the JWT NameIdentifier claim.
    ///
    /// Why ClaimTypes.NameIdentifier?
    ///   JwtTokenService writes the user's Id into the "sub" (Subject) claim, which
    ///   ASP.NET Core's JWT Bearer middleware maps to ClaimTypes.NameIdentifier when
    ///   it validates and unpacks the token. Reading NameIdentifier is therefore the
    ///   standard way to get the subject claim from an ASP.NET Core ClaimsPrincipal.
    ///
    /// Why throw instead of returning null or Guid.Empty on a missing claim?
    ///   This method is only called from [Authorize]-protected endpoints. The JWT
    ///   Bearer middleware has already validated the token and populated User before
    ///   any controller action runs — if the NameIdentifier claim is absent, the token
    ///   was issued without a subject claim, which is a bug in JwtTokenService, not a
    ///   runtime condition the controller should handle gracefully. Throwing surfaces
    ///   the programming error immediately rather than silently processing a request
    ///   with an empty or zero user ID.
    ///
    /// Why return Guid rather than string?
    ///   The User entity's Id is a Guid. Returning Guid here enforces the type at the
    ///   boundary — callers never receive a raw string that could be passed to the wrong
    ///   parameter or silently fail a Guid.Parse later.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        // FindFirstValue returns null if the claim is absent — throw immediately
        // rather than propagating null into handler code.
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("UserId claim is missing from the token.");

        return Guid.Parse(value);
    }
}
