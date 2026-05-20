using DMS.Domain.Enums;

namespace DMS.Application.DTOs;

public sealed record SharedResourceDto(
    Guid Id,
    string Name,
    ShareResourceType ResourceType,
    ShareRole Role,
    Guid SharedByUserId,
    DateTimeOffset SharedAt);
