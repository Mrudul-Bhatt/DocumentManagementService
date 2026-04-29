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

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, ct);

        // Deliberately vague: don't reveal whether the email exists or the password was wrong
        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for email {Email} from IP {IpAddress}",
                command.Email, command.IpAddress);
            return Result.Failure<AuthTokensDto>(DomainErrors.User.InvalidCredentials);
        }

        if (!user.IsActive)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.Suspended);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var refreshToken = DomainRefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays));
        await refreshTokenRepository.AddAsync(refreshToken, ct);

        logger.LogInformation("User {UserId} logged in from IP {IpAddress}", user.Id, command.IpAddress);

        return Result.Success(new AuthTokensDto(accessToken, rawRefreshToken, _jwt.ExpiryMinutes * 60));
    }
}
