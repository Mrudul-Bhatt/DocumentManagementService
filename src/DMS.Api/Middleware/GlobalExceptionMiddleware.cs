using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Middleware;

/// <summary>
/// Last-resort exception handler that converts any unhandled exception into a
/// standards-compliant RFC 7807 ProblemDetails response.
///
/// Where this fits in the ASP.NET Core pipeline:
///   Middleware is arranged as a chain of nested delegates. This middleware wraps
///   every component that follows it (routing, auth, controllers, etc.) in a
///   try/catch. Any exception that propagates all the way out of that chain lands
///   here rather than crashing the server or leaking a stack trace to the caller.
///
///   Registration order in Program.cs matters: this must be added FIRST so it
///   wraps all other middleware. Adding it last would leave most of the pipeline
///   unprotected.
///
/// Design decisions:
///   - Logs the full exception (stack trace included) for server-side diagnostics.
///   - Returns a deliberately vague message to the client — never expose internal
///     details like stack traces, file paths, or database errors to callers.
///   - Always returns 500: if the exception was unhandled, we genuinely don't know
///     what happened, so 500 is the honest and correct status.
///
/// Why not use ASP.NET Core's built-in UseExceptionHandler()?
///   UseExceptionHandler() is a valid alternative. A custom middleware is used here
///   for explicitness — the exact logging format, response shape, and Content-Type
///   are all visible in one place rather than scattered across configuration.
///
/// Why not catch exceptions in each controller instead?
///   Controllers should not handle unexpected exceptions — that conflates two
///   responsibilities. Controllers handle *expected* failures via the Result pattern.
///   This middleware handles *unexpected* exceptions (bugs, infrastructure faults)
///   that no controller anticipated. Keeping these concerns separate means no
///   controller ever needs a try/catch.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    /// <summary>
    /// Called by the ASP.NET Core pipeline for every incoming request.
    ///
    /// Invokes the next middleware in the chain. If any exception escapes that chain,
    /// it is caught here: the exception is logged and a 500 ProblemDetails response
    /// is written. The request is considered handled — the exception does not
    /// propagate further.
    /// </summary>
    /// <param name="context">The current HTTP request/response context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass control to the rest of the pipeline (routing → auth → controllers).
            // In the successful path this middleware is invisible — it adds zero overhead
            // once the try block completes without throwing.
            await next(context);
        }
        catch (Exception ex)
        {
            // LogError includes the exception object, which captures the full stack trace
            // in structured logging. {Method} and {Path} appear as searchable fields in
            // log aggregation tools (e.g., Seq, Elastic) so incidents can be filtered
            // by endpoint without parsing the message string.
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteProblemResponseAsync(context, ex);
        }
    }

    /// <summary>
    /// Writes an RFC 7807 ProblemDetails JSON body directly to the HTTP response.
    ///
    /// Why write directly to the response instead of throwing an IActionResult?
    ///   Middleware runs outside the MVC pipeline — there is no controller context,
    ///   no ObjectResult serialisation, and no content negotiation. The response
    ///   must be constructed and written manually using the raw HttpResponse API.
    ///
    /// Why set Content-Type manually?
    ///   RFC 7807 requires Content-Type: application/problem+json for ProblemDetails
    ///   responses. Without this, clients may not recognise the response as a
    ///   structured error and fall back to treating it as plain JSON or an error string.
    ///
    /// Why is the Detail message vague ("An unexpected error occurred")?
    ///   Security hygiene: internal exception messages may contain database schema
    ///   names, file paths, connection strings, or other sensitive information.
    ///   Returning a generic message to the client prevents information disclosure.
    ///   The full exception is available in server-side logs for the developer.
    ///
    /// The <paramref name="ex"/> parameter is intentionally unused in the response body.
    /// It exists so the signature remains consistent if detailed logging per exception
    /// type is added in a future level (e.g., returning a correlation ID or logging
    /// a request trace).
    /// </summary>
    private static async Task WriteProblemResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Must be set before writing the body — headers cannot be changed after
        // the response has started streaming.
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
            Status = StatusCodes.Status500InternalServerError
        };

        // JsonSerializer.Serialize produces a UTF-8 JSON string.
        // WriteAsync writes it to the response body stream and flushes.
        // No System.Text.Json options are needed here — ProblemDetails only
        // contains primitive string and int properties.
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
