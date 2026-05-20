using DMS.Domain.Enums;

namespace DMS.Application.DTOs;

public sealed record ShareDto(
    Guid Id,
    Guid ResourceId,
    ShareResourceType ResourceType,
    Guid GrantedToUserId,
    string GrantedToEmail,
    ShareRole Role,
    DateTimeOffset CreatedAt);
