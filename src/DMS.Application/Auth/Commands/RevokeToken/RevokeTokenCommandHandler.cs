using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Auth.Commands.RevokeToken;

/// <summary>
/// Handles RevokeTokenCommand: marks a refresh token as revoked (explicit logout).
/// </summary>
internal sealed class RevokeTokenCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    : IRequestHandler<RevokeTokenCommand, Result>
{
    /// <summary>
    /// Executes the revocation.
    ///
    /// Flow:
    ///   1. Look up the token by its raw string value.
    ///   2. Reject if not found or already inactive (revoked or expired).
    ///   3. Call token.Revoke() to set RevokedAt on the domain entity.
    ///   4. Persist the updated entity and return success.
    ///
    /// Why reject an already-inactive token instead of treating it as a no-op?
    ///   If a client sends a token that is already revoked or expired, it likely
    ///   indicates the client has stale state (e.g., replaying a logout request).
    ///   Returning Token.Invalid (401) prompts the client to re-authenticate rather
    ///   than silently accepting the call as a success — clearer error signalling.
    ///
    /// Why no user lookup here (unlike RefreshTokenCommandHandler)?
    ///   Revoke does not need to re-issue tokens or check account status. The only
    ///   requirement is that the token exists and is currently active. Loading the
    ///   user would be an unnecessary database round-trip.
    ///
    /// Note: the access token issued alongside this refresh token is NOT revoked here.
    ///   Access tokens are stateless JWTs — there is no server-side revocation mechanism
    ///   without a denylist (added in a future level). The access token will continue
    ///   working until it expires naturally (default 60 min). This is an accepted trade-off
    ///   of the stateless JWT model: true instant revocation requires a denylist or very
    ///   short token lifetimes.
    /// </summary>
    public async Task<Result> Handle(RevokeTokenCommand command, CancellationToken ct)
    {
        var token = await refreshTokenRepository.GetByTokenAsync(command.Token, ct);

        if (token is null || !token.IsActive)
            return Result.Failure(DomainErrors.Token.Invalid);

        // Revoke() sets RevokedAt = DateTimeOffset.UtcNow on the domain entity.
        // IsActive will return false for this token from this point forward.
        token.Revoke();
        await refreshTokenRepository.UpdateAsync(token, ct);

        return Result.Success();
    }
}
