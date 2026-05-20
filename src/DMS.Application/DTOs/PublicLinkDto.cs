using DMS.Domain.Enums;

namespace DMS.Application.DTOs;

public sealed record PublicLinkDto(
    Guid Id,
    Guid ResourceId,
    ShareResourceType ResourceType,
    string Token,
    ShareRole Role,
    DateTimeOffset? ExpiresAt,
    bool HasPassword,
    DateTimeOffset CreatedAt);
