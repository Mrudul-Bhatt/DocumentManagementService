namespace DMS.Application.Services;

public interface IEmailService
{
    // TODO: wire up a real email provider (SendGrid / SMTP) in DMS.Infrastructure
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default);
}
