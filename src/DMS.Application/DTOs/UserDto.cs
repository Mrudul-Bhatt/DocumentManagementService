using DMS.Domain.Enums;

namespace DMS.Application.DTOs;

public sealed record UserDto(
    Guid Id,
    string Email,
    Role Role,
    bool IsActive,
    DateTimeOffset CreatedAt);
