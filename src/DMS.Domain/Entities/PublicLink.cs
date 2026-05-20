using DMS.Domain.Enums;

namespace DMS.Domain.Entities;

/// <summary>
/// Token-based unauthenticated share. Anyone with the token URL can access the
/// resource at the link's role level — no account required.
///
/// Token is a 64-character lowercase hex string derived from 32 cryptographically
/// random bytes. The unique index on Token makes lookup O(log n).
/// </summary>
public sealed class PublicLink
{
    public Guid Id { get; private set; }

    /// <summary>The file or folder this link points to.</summary>
    public Guid ResourceId { get; private set; }

    public ShareResourceType ResourceType { get; private set; }

    /// <summary>64-char hex token. Embedded in the shareable URL.</summary>
    public string Token { get; private set; } = default!;

    /// <summary>Access level anyone with this link receives.</summary>
    public ShareRole Role { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    /// <summary>Null means the link never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Bcrypt hash of the link password. Null means no password is required.</summary>
    public string? PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private PublicLink() { }

    public static PublicLink Create(
        Guid resourceId,
        ShareResourceType resourceType,
        string token,
        ShareRole role,
        Guid createdByUserId,
        DateTimeOffset? expiresAt = null,
        string? passwordHash = null)
    {
        return new PublicLink
        {
            Id               = Guid.NewGuid(),
            ResourceId       = resourceId,
            ResourceType     = resourceType,
            Token            = token,
            Role             = role,
            CreatedByUserId  = createdByUserId,
            ExpiresAt        = expiresAt,
            PasswordHash     = passwordHash,
            CreatedAt        = DateTimeOffset.UtcNow,
        };
    }

    public bool IsExpired() => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;

    public bool RequiresPassword() => PasswordHash is not null;
}
