using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Auth.Commands.RefreshToken;

/// <summary>
/// CQRS command that exchanges a valid refresh token for a new access token + refresh token pair.
///
/// Why a Command (write operation) rather than a Query (read operation)?
///   Token refresh mutates state — the existing refresh token is revoked and a new
///   one is issued (refresh token rotation). Any operation that changes state is a
///   Command in CQRS, even if it returns a value. Queries must be side-effect-free.
///
/// Single parameter design:
///   Only the raw token string is needed. The handler resolves the associated user
///   by looking up the token in the database — the caller does not need to supply UserId.
///   This prevents a caller from combining someone else's UserId with a stolen token.
/// </summary>
public sealed record RefreshTokenCommand(
    /// <summary>The raw refresh token string previously issued by login or a prior refresh.</summary>
    string Token) : IRequest<Result<AuthTokensDto>>;
