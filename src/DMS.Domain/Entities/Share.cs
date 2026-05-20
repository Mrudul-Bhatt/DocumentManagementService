using DMS.Domain.Enums;

namespace DMS.Domain.Entities;

/// <summary>
/// ACL row that grants a specific registered user access to a file or folder.
/// One row = one (resource, grantee, role) triple.
/// A unique constraint on (ResourceId, GrantedToUserId) prevents duplicate grants.
/// </summary>
public sealed class Share
{
    public Guid Id { get; private set; }

    /// <summary>The file or folder being shared.</summary>
    public Guid ResourceId { get; private set; }

    public ShareResourceType ResourceType { get; private set; }

    /// <summary>The user receiving access.</summary>
    public Guid GrantedToUserId { get; private set; }

    /// <summary>The owner who created this share.</summary>
    public Guid GrantedByUserId { get; private set; }

    public ShareRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Share() { }

    public static Share Create(
        Guid resourceId,
        ShareResourceType resourceType,
        Guid grantedToUserId,
        Guid grantedByUserId,
        ShareRole role)
    {
        return new Share
        {
            Id               = Guid.NewGuid(),
            ResourceId       = resourceId,
            ResourceType     = resourceType,
            GrantedToUserId  = grantedToUserId,
            GrantedByUserId  = grantedByUserId,
            Role             = role,
            CreatedAt        = DateTimeOffset.UtcNow,
        };
    }

    public bool IsGrantedTo(Guid userId) => GrantedToUserId == userId;
}
