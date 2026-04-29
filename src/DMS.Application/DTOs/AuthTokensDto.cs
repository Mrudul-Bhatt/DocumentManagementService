namespace DMS.Application.DTOs;

public sealed record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);
