using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Auth.Commands.Login;

/// <summary>
/// CQRS command that validates credentials and issues a JWT access token + refresh token pair.
///
/// Why does LoginCommand not implement IAuditableRequest?
///   IAuditableRequest is designed for auditing successful operations on protected resources
///   (file uploads, downloads, deletes). Login is an authentication event — success and
///   failure are both logged directly in the handler via LogInformation / LogWarning with
///   structured fields (Email, IpAddress). Routing it through AuditLoggingBehaviour would
///   duplicate the logging and write to the AuditLog table, which is intended for
///   post-authentication resource actions, not authentication events themselves.
///
/// Why is IpAddress nullable?
///   IP may be unavailable behind certain reverse proxies or in test environments.
///   The handler uses it only for structured warning logs on failed attempts — a null IP
///   degrades the log entry but does not block authentication.
/// </summary>
public sealed record LoginCommand(
    /// <summary>Email address to look up. Case-insensitive — handler compares against lowercased stored value.</summary>
    string Email,

    /// <summary>Plaintext password. Verified against the stored BCrypt hash; never logged or stored.</summary>
    string Password,

    /// <summary>Caller's IP address for logging failed login attempts. Nullable — see class summary.</summary>
    string? IpAddress) : IRequest<Result<AuthTokensDto>>;
