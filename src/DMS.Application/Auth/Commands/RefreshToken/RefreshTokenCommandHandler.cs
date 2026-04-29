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

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions)
    : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var existing = await refreshTokenRepository.GetByTokenAsync(command.Token, ct);

        if (existing is null || !existing.IsActive)
            return Result.Failure<AuthTokensDto>(DomainErrors.Token.Invalid);

        var user = await userRepository.GetByIdAsync(existing.UserId, ct);

        if (user is null)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.NotFound);

        if (!user.IsActive)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.Suspended);

        // Rotate: revoke the used token and issue a new pair
        existing.Revoke();
        await refreshTokenRepository.UpdateAsync(existing, ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var newRefreshToken = DomainRefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays));
        await refreshTokenRepository.AddAsync(newRefreshToken, ct);

        return Result.Success(new AuthTokensDto(accessToken, rawRefreshToken, _jwt.ExpiryMinutes * 60));
    }
}
