using DMS.Application.Common;
using DMS.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Extensions;

/// <summary>
/// Extension methods that translate the application's Result pattern into HTTP responses.
///
/// Responsibility boundary:
///   This class owns the single translation point between domain/application outcomes
///   and HTTP semantics. Controllers call ToProblemResult() and never construct
///   ProblemDetails or status codes themselves — all that mapping logic lives here.
///
/// Why an extension method instead of a base controller?
///   Extension methods keep the mapping logic out of the controller inheritance hierarchy.
///   Controllers stay thin; this class is easy to extend independently when new error
///   codes are introduced in a new level.
///
/// Why RFC 7807 ProblemDetails?
///   ProblemDetails is the HTTP standard for machine-readable error responses. It gives
///   API consumers a consistent shape (title, detail, status) regardless of which
///   endpoint failed, making automated error handling reliable on the client side.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps every known domain error code to its canonical HTTP status code.
    ///
    /// Level 1 additions over Level 0:
    ///   User.NotFound          — user looked up by ID no longer exists → 404
    ///   User.EmailAlreadyExists — duplicate registration attempt → 409 Conflict
    ///   User.InvalidCredentials — wrong email or password (deliberately vague) → 401
    ///   User.Suspended          — account suspended by admin → 403
    ///   Token.Invalid           — refresh token not found or already revoked → 401
    ///   Token.Expired           — refresh token past its expiry date → 401
    ///
    /// Why 401 for InvalidCredentials rather than 400?
    ///   HTTP 401 means "authentication failed or not provided". A wrong password is
    ///   an authentication failure, so 401 is semantically correct. 400 would imply
    ///   the request was malformed, which is misleading when the shape is valid but
    ///   the credentials are wrong.
    ///
    /// Why 403 for Suspended rather than 401?
    ///   401 means "not authenticated". A suspended user is authenticated (we know who
    ///   they are) but not authorised to proceed — 403 Forbidden is the correct code.
    ///
    /// Why 409 for EmailAlreadyExists?
    ///   409 Conflict means "the request could not be completed due to a conflict with
    ///   the current state of the resource". A duplicate email is exactly that — the
    ///   desired registration conflicts with an existing account.
    /// </summary>
    private static readonly Dictionary<string, int> ErrorStatusCodes = new()
    {
        [DomainErrors.File.NotFound.Code]                  = StatusCodes.Status404NotFound,
        [DomainErrors.File.Forbidden.Code]                 = StatusCodes.Status403Forbidden,
        [DomainErrors.File.TooLarge.Code]                  = StatusCodes.Status413RequestEntityTooLarge,
        [DomainErrors.File.Empty.Code]                     = StatusCodes.Status400BadRequest,
        [DomainErrors.User.NotFound.Code]                  = StatusCodes.Status404NotFound,
        [DomainErrors.User.EmailAlreadyExists.Code]        = StatusCodes.Status409Conflict,
        [DomainErrors.User.InvalidCredentials.Code]        = StatusCodes.Status401Unauthorized,
        [DomainErrors.User.Suspended.Code]                 = StatusCodes.Status403Forbidden,
        [DomainErrors.Token.Invalid.Code]                  = StatusCodes.Status401Unauthorized,
        [DomainErrors.Token.Expired.Code]                  = StatusCodes.Status401Unauthorized,
        [DomainErrors.Folder.NotFound.Code]                = StatusCodes.Status404NotFound,
        [DomainErrors.Folder.Forbidden.Code]               = StatusCodes.Status403Forbidden,
        [DomainErrors.Folder.MaxDepthExceeded.Code]        = StatusCodes.Status422UnprocessableEntity,
        [DomainErrors.Folder.NameConflict.Code]            = StatusCodes.Status409Conflict,
        [DomainErrors.FileVersion.NotFound.Code]           = StatusCodes.Status404NotFound,
        [DomainErrors.FileVersion.CannotDeleteCurrent.Code] = StatusCodes.Status409Conflict,
    };

    /// <summary>
    /// Converts a failed Result (or Result&lt;T&gt;) into an RFC 7807 ProblemDetails HTTP response.
    ///
    /// Result&lt;T&gt; inherits from Result, so this single non-generic method handles both
    /// void failures (e.g., revoke) and value failures (e.g., login) uniformly.
    ///
    /// Unmapped error codes fall back to 500 — signals a missing entry in ErrorStatusCodes
    /// that should be added explicitly rather than silently returning a misleading 4xx.
    /// </summary>
    public static IActionResult ToProblemResult(this Result result, ControllerBase controller)
    {
        var statusCode = ErrorStatusCodes.GetValueOrDefault(result.Error.Code, StatusCodes.Status500InternalServerError);

        return controller.Problem(
            detail: result.Error.Description,
            title: result.Error.Code,
            statusCode: statusCode);
    }
}
