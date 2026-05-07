namespace DMS.Domain.Entities;

public sealed class Folder
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public Guid OwnerId { get; private set; }
    public Guid? ParentFolderId { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    private Folder() { }

    public static Folder Create(string name, Guid ownerId, Guid? parentFolderId = null)
    {
        return new Folder
        {
            Id             = Guid.NewGuid(),
            Name           = name,
            OwnerId        = ownerId,
            ParentFolderId = parentFolderId,
            CreatedAt      = DateTimeOffset.UtcNow
        };
    }

    public void Rename(string newName)  => Name      = newName;
    public void SoftDelete()            => DeletedAt = DateTimeOffset.UtcNow;
    public void Restore()               => DeletedAt = null;
    public void MoveTo(Guid? parentId)  => ParentFolderId = parentId;
    public bool BelongsTo(Guid userId)  => OwnerId == userId;
}
