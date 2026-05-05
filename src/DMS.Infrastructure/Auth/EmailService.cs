using DMS.Application.Services;
using Microsoft.Extensions.Logging;

namespace DMS.Infrastructure.Auth;

/// <summary>
/// No-op email service that logs the email content instead of sending it.
///
/// This is a development stub — Level 1 defines the IEmailService interface and wires
/// it into DI so that command handlers can depend on it, but no real email provider
/// (SendGrid, AWS SES, SMTP) is configured yet. The stub allows the auth flow to be
/// exercised end-to-end in development without requiring external credentials.
///
/// Why log instead of silently discard?
///   Logging the email content makes it visible during local development and integration
///   testing without requiring a real inbox. The [EMAIL STUB] prefix makes it immediately
///   obvious in log output that this is not a production send.
///
/// Why Scoped lifetime (registered in DependencyInjection)?
///   ILogger<T> is injected, and while ILogger itself is thread-safe, the scoped
///   lifetime aligns with the request scope for consistent log correlation (e.g., trace
///   IDs). When a real email provider is implemented, it may need scoped dependencies
///   (e.g., a per-request HTTP client from IHttpClientFactory).
///
/// TODO: Replace with a real email provider (SendGrid / SMTP) before deploying to production.
/// </summary>
internal sealed class EmailService(ILogger<EmailService> logger) : IEmailService
{
    /// <summary>
    /// Logs the password reset email content instead of sending it.
    ///
    /// Named placeholders ({Email}, {ResetLink}) are used rather than string interpolation
    /// so Serilog captures them as structured log properties — queryable in log aggregators
    /// like Seq or Application Insights. String interpolation would flatten them into an
    /// unstructured message string.
    ///
    /// Returns Task.CompletedTask (a cached completed task) rather than creating a new
    /// Task — no async/await needed because there is no actual I/O to perform.
    /// </summary>
    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL STUB] Password reset for {Email} — link: {ResetLink}",
            toEmail, resetLink);

        return Task.CompletedTask;
    }
}
