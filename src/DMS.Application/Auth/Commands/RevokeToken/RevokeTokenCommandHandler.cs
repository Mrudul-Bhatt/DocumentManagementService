using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Auth.Commands.RevokeToken;

internal sealed class RevokeTokenCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    : IRequestHandler<RevokeTokenCommand, Result>
{
    public async Task<Result> Handle(RevokeTokenCommand command, CancellationToken ct)
    {
        var token = await refreshTokenRepository.GetByTokenAsync(command.Token, ct);

        if (token is null || !token.IsActive)
            return Result.Failure(DomainErrors.Token.Invalid);

        token.Revoke();
        await refreshTokenRepository.UpdateAsync(token, ct);

        return Result.Success();
    }
}
