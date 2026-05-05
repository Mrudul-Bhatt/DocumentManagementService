using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Application.Services;
using DMS.Application.Settings;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DomainRefreshToken = DMS.Domain.Entities.RefreshToken;
using DMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMS.Application.Auth.Commands.Login;

/// <summary>
/// Handles LoginCommand: validates credentials and issues a JWT access + refresh token pair.
/// </summary>
internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Executes the login flow.
    ///
    /// Flow:
    ///   1. Look up the user by email.
    ///   2. If not found or password wrong — log the failed attempt and return InvalidCredentials.
    ///   3. If account is suspended — return Suspended.
    ///   4. Issue access token + refresh token, persist the refresh token, return both.
    ///
    /// Why merge "email not found" and "wrong password" into a single InvalidCredentials error?
    ///   User enumeration: if the API returned different errors for these two cases, an attacker
    ///   could determine which emails are registered by probing the endpoint. A single vague
    ///   error forces the attacker to brute-force both the email and the password simultaneously,
    ///   making automated attacks significantly harder.
    ///
    /// Why check IsActive after the credential check (not before)?
    ///   Checking account status before verifying credentials would reveal that the email
    ///   exists (because only valid accounts can be suspended). Credential verification
    ///   must come first to preserve the user enumeration protection above.
    ///
    /// Why LogWarning on failure?
    ///   Failed login attempts are security-relevant events. Warning level ensures they
    ///   appear in production logs with the minimum log level typically configured for
    ///   alerts, without being as severe as an unhandled exception (Error/Critical).
    /// </summary>
    public async Task<Result<AuthTokensDto>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, ct);

        // Single branch for "user not found" AND "wrong password" — see user enumeration note above.
        // BCrypt.Verify is only called when user is non-null; short-circuit evaluation prevents
        // a NullReferenceException while preserving the single error response.
        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for email {Email} from IP {IpAddress}",
                command.Email, command.IpAddress);
            return Result.Failure<AuthTokensDto>(DomainErrors.User.InvalidCredentials);
        }

        // IsActive check comes after credential verification — see ordering rationale above.
        if (!user.IsActive)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.Suspended);

        var accessToken    = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var refreshToken = DomainRefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays));
        await refreshTokenRepository.AddAsync(refreshToken, ct);

        logger.LogInformation("User {UserId} logged in from IP {IpAddress}", user.Id, command.IpAddress);

        return Result.Success(new AuthTokensDto(accessToken, rawRefreshToken, _jwt.ExpiryMinutes * 60));
    }
}
