using DMS.Domain.Enums;

namespace DMS.Application.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, Role role);
    string GenerateRefreshToken();
}
