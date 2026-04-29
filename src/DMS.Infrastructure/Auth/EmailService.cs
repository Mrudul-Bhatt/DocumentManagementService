using DMS.Application.Services;
using Microsoft.Extensions.Logging;

namespace DMS.Infrastructure.Auth;

// TODO: Replace with a real email provider (SendGrid / SMTP) when deploying
internal sealed class EmailService(ILogger<EmailService> logger) : IEmailService
{
    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL STUB] Password reset for {Email} — link: {ResetLink}",
            toEmail, resetLink);

        return Task.CompletedTask;
    }
}
