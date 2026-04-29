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

namespace DMS.Application.Auth.Commands.Register;

internal sealed class RegisterCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions,
    ILogger<RegisterCommandHandler> logger)
    : IRequestHandler<RegisterCommand, Result<AuthTokensDto>>
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task<Result<AuthTokensDto>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(command.Email, ct);
        if (existing is not null)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.EmailAlreadyExists);

        var passwordHash = passwordHasher.Hash(command.Password);
        var user = User.Create(command.Email, passwordHash);
        await userRepository.AddAsync(user, ct);

        var tokens = await IssueTokenPairAsync(user, ct);

        logger.LogInformation("User {UserId} registered with email {Email}", user.Id, user.Email);

        return Result.Success(tokens);
    }

    private async Task<AuthTokensDto> IssueTokenPairAsync(User user, CancellationToken ct)
    {
        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var refreshToken = DomainRefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays));
        await refreshTokenRepository.AddAsync(refreshToken, ct);

        return new AuthTokensDto(accessToken, rawRefreshToken, _jwt.ExpiryMinutes * 60);
    }
}
