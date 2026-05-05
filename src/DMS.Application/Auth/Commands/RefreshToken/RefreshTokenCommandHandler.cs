using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Application.Services;
using DMS.Application.Settings;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DomainRefreshToken = DMS.Domain.Entities.RefreshToken;
using DMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace DMS.Application.Auth.Commands.RefreshToken;

/// <summary>
/// Handles RefreshTokenCommand: validates a refresh token, rotates it, and issues a new pair.
/// </summary>
internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions)
    : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Executes the token refresh flow.
    ///
    /// Flow:
    ///   1. Look up the refresh token by its raw string value.
    ///   2. Reject if not found or not active (already revoked or expired).
    ///   3. Load the owning user — reject if deleted or suspended.
    ///   4. Revoke the presented token (rotation step).
    ///   5. Generate and persist a new refresh token.
    ///   6. Generate a new access token and return both.
    ///
    /// Why return Token.Invalid for both "not found" and "not active"?
    ///   Distinguishing between "this token never existed" and "this token was revoked"
    ///   would help an attacker determine whether a stolen token has been detected.
    ///   A single vague error gives no information about the token's history.
    ///
    /// What is refresh token rotation?
    ///   Every call to this handler revokes the presented token and issues a new one.
    ///   If the same refresh token is presented twice, the second call fails with Token.Invalid
    ///   (the first call already revoked it). This means:
    ///   - A stolen token can only be used once before the legitimate owner's next refresh
    ///     reveals the compromise (their token was consumed by the attacker).
    ///   - Without rotation, a stolen token is silently valid for its entire lifetime.
    ///
    /// Why check user.IsActive after token validation?
    ///   The token's owner must be an active account. A suspended user's tokens are not
    ///   individually revoked at suspension time — the IsActive check here enforces the
    ///   suspension at every refresh attempt without requiring a mass token revocation.
    /// </summary>
    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var existing = await refreshTokenRepository.GetByTokenAsync(command.Token, ct);

        // IsActive checks both IsRevoked and IsExpired — see RefreshToken.IsActive in domain.
        if (existing is null || !existing.IsActive)
            return Result.Failure<AuthTokensDto>(DomainErrors.Token.Invalid);

        var user = await userRepository.GetByIdAsync(existing.UserId, ct);

        if (user is null)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.NotFound);

        if (!user.IsActive)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.Suspended);

        // Rotation step: mark the used token as revoked before issuing a replacement.
        // This ensures the old token cannot be re-used even if the response is intercepted.
        existing.Revoke();
        await refreshTokenRepository.UpdateAsync(existing, ct);

        var accessToken    = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var newRefreshToken = DomainRefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays));
        await refreshTokenRepository.AddAsync(newRefreshToken, ct);

        return Result.Success(new AuthTokensDto(accessToken, rawRefreshToken, _jwt.ExpiryMinutes * 60));
    }
}
