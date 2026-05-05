using DMS.Domain.Enums;

namespace DMS.Application.DTOs;

/// <summary>
/// Read-only projection of a user account returned to the API layer.
///
/// Why no PasswordHash field?
///   PasswordHash is an internal security credential — it must never leave the
///   server. Projecting the domain User entity to this DTO is the enforcement
///   mechanism: the API layer receives only this flat record, which structurally
///   cannot carry the hash. Even if a developer accidentally tried to expose it,
///   the field simply does not exist on this type.
///
/// Used by:
///   RegisterCommandHandler — returned on successful registration (201 Created body).
///   Future admin endpoints — for listing or inspecting user accounts.
/// </summary>
public sealed record UserDto(
    /// <summary>Unique identifier of the user account.</summary>
    Guid Id,

    /// <summary>Email address stored in lowercase. Used as the login identifier.</summary>
    string Email,

    /// <summary>The user's role (User / Admin). Embedded in the JWT access token as the "role" claim.</summary>
    Role Role,

    /// <summary>
    /// Whether the account is active. Inactive (suspended) accounts cannot log in.
    /// Suspension is applied via User.Suspend() in the domain entity.
    /// </summary>
    bool IsActive,

    /// <summary>UTC timestamp of when the account was created.</summary>
    DateTimeOffset CreatedAt);
