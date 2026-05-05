using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Auth.Commands.RevokeToken;

/// <summary>
/// CQRS command that permanently revokes a refresh token, preventing future use.
///
/// Why IRequest&lt;Result&gt; (non-generic)?
///   Revoke is a void operation — on success there is nothing to return.
///   The controller maps success to 204 No Content.
///
/// Use case — explicit logout:
///   When a user logs out, the client sends the stored refresh token to this endpoint.
///   The token is marked revoked in the database. Even if the token string is later
///   stolen, it can no longer be used to obtain a new access token.
/// </summary>
public sealed record RevokeTokenCommand(
    /// <summary>The raw refresh token string to revoke.</summary>
    string Token) : IRequest<Result>;
