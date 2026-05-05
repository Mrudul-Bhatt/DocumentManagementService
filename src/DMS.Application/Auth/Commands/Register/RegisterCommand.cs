using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Auth.Commands.Register;

/// <summary>
/// CQRS command that creates a new user account and returns a token pair on success.
///
/// Why return AuthTokensDto on registration instead of just the UserDto?
///   Immediately issuing tokens after registration eliminates the extra login step
///   that would otherwise follow. The user is authenticated the moment they register —
///   returning a token pair makes their session start seamlessly.
///
/// Why no IpAddress / IAuditableRequest here?
///   Registration is already captured as a structured log event in the handler
///   (LogInformation with UserId and Email). A separate audit log entry for
///   registration is not required at this level — the user record itself in the
///   database is the durable record of the registration event.
/// </summary>
public sealed record RegisterCommand(
    /// <summary>Email address for the new account. Stored lowercased. Must be unique.</summary>
    string Email,

    /// <summary>Plaintext password. Hashed with BCrypt (cost 12) before storage — never stored as-is.</summary>
    string Password) : IRequest<Result<AuthTokensDto>>;
