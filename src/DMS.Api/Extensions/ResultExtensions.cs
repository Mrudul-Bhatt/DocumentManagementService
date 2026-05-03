using DMS.Application.Common;
using DMS.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Extensions;

/// <summary>
/// Extension methods that translate the application's Result pattern into HTTP responses.
///
/// Responsibility boundary:
///   This class owns the single translation point between domain/application outcomes and
///   HTTP semantics. Controllers call ToProblemResult() and never construct ProblemDetails
///   or status codes themselves — all that logic lives here.
///
/// Why an extension method instead of a base controller?
///   Extension methods keep the mapping logic out of the controller inheritance hierarchy.
///   Controllers stay thin and testable; this class is easy to update independently when
///   new error codes or status code mappings are introduced.
///
/// Why RFC 7807 ProblemDetails?
///   ProblemDetails is the HTTP standard for machine-readable error responses. It gives
///   API consumers a consistent shape (title, detail, status) regardless of which endpoint
///   failed, which makes automated error handling reliable on the client side.
///   ASP.NET Core's controller.Problem() helper builds a valid RFC 7807 body and sets the
///   Content-Type response header to "application/problem+json" automatically.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps known domain error codes to their canonical HTTP status codes.
    ///
    /// Why a dictionary lookup instead of a switch expression?
    ///   Error codes are strings defined as constants in DomainErrors. A dictionary
    ///   lookup scales cleanly as new errors are introduced: add one line here and the
    ///   entire API picks up the correct status code. A switch would require changes in
    ///   two places (the new error definition AND a new case here), which risks drift.
    ///
    /// Why static readonly?
    ///   The mapping never changes at runtime. Allocating it once at class load avoids
    ///   re-constructing the dictionary on every request.
    ///
    /// Why default to 500 for unmapped codes?
    ///   An error code that isn't in this map means the Application or Domain layer
    ///   returned a failure that the API layer didn't account for. Returning 500 is
    ///   honest: something unexpected happened. It also surfaces the gap during
    ///   development rather than silently sending a misleading 4xx.
    ///   (When a new error code is added, it should be mapped here explicitly.)
    /// </summary>
    private static readonly Dictionary<string, int> ErrorStatusCodes = new()
    {
        [DomainErrors.File.NotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.File.Forbidden.Code] = StatusCodes.Status403Forbidden,
        [DomainErrors.File.TooLarge.Code] = StatusCodes.Status413RequestEntityTooLarge,
        [DomainErrors.File.Empty.Code] = StatusCodes.Status400BadRequest,
        [DomainErrors.User.IdMissing.Code] = StatusCodes.Status400BadRequest,
    };

    /// <summary>
    /// Converts a failed <see cref="Result"/> (or <see cref="Result{T}"/>) into an RFC 7807
    /// ProblemDetails HTTP response.
    ///
    /// Why does this accept the base non-generic <c>Result</c>?
    ///   <see cref="Result{T}"/> inherits from <see cref="Result"/>, so a single method
    ///   handles both the void-failure case (e.g., delete) and the value-failure case
    ///   (e.g., upload). Controllers call this method the same way regardless of whether
    ///   the result carries a value type.
    ///
    /// Why does this take a <c>ControllerBase</c> parameter?
    ///   <c>controller.Problem()</c> is an instance method on ControllerBase that builds a
    ///   correctly formatted RFC 7807 response AND sets Content-Type to
    ///   "application/problem+json". Using it ensures the response is standards-compliant
    ///   without writing that header logic here manually.
    ///
    /// ProblemDetails shape returned:
    ///   {
    ///     "title":  "File.NotFound",           ← machine-readable error code
    ///     "detail": "The requested file...",   ← human-readable message
    ///     "status": 404                        ← mirrors the HTTP status code
    ///   }
    ///
    /// Why put the error code in "title" and the description in "detail"?
    ///   RFC 7807 defines "title" as a short, stable string clients can match on, and
    ///   "detail" as a human-readable explanation. Using the error code (e.g., "File.NotFound")
    ///   as the title lets clients branch on a stable identifier without parsing the
    ///   human-readable message (which may change over time).
    /// </summary>
    /// <param name="result">The failed result whose error will be translated.</param>
    /// <param name="controller">The calling controller, used to build the RFC 7807 response.</param>
    /// <returns>An <see cref="IActionResult"/> with the appropriate HTTP status code and ProblemDetails body.</returns>
    public static IActionResult ToProblemResult(this Result result, ControllerBase controller)
    {
        // Resolve the HTTP status code for this specific domain error.
        // Falls back to 500 for any error code not explicitly mapped above —
        // signals a missing entry in ErrorStatusCodes rather than hiding the gap.
        var statusCode = ErrorStatusCodes.GetValueOrDefault(result.Error.Code, StatusCodes.Status500InternalServerError);

        // controller.Problem() constructs a valid RFC 7807 ProblemDetails object,
        // sets the HTTP status code on the response, and sets Content-Type to
        // "application/problem+json". This is preferred over manually constructing
        // ObjectResult(new ProblemDetails { ... }) for the same reason.
        return controller.Problem(
            detail: result.Error.Description,
            title: result.Error.Code,
            statusCode: statusCode);
    }
}
