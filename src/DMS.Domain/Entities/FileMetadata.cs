namespace DMS.Domain.Entities;

/// <summary>
/// Domain entity that records the metadata of an uploaded file.
///
/// Why is the file content not stored here?
///   Entities carry identity and domain rules, not raw bytes. Storing file content on
///   the entity would force EF Core to load potentially hundreds of megabytes into memory
///   whenever this entity is fetched — even for operations that only need the filename.
///   The binary content lives in the storage backend (local disk in Level 0/1, blob
///   storage in later levels); StoragePath is the opaque pointer to it.
///
/// Why sealed?
///   Domain entities enforce invariants through private setters and the Create() factory.
///   Subclassing could bypass these guarantees, so sealed is the safe default.
///
/// Why private setters on all properties?
///   All state changes must be expressed as domain methods, keeping business rules
///   localised on the entity. No external code can mutate a property directly.
/// </summary>
public sealed class FileMetadata
{
    /// <summary>Application-generated primary key. EF config uses ValueGeneratedNever() — EF does not auto-generate this server-side.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// String representation of the owning user's Guid. This is the foreign key to the
    /// User entity. BelongsTo() uses this to enforce ownership checks in command handlers
    /// before any mutating operation (download or delete).
    /// </summary>
    public string UserId { get; private set; } = default!;

    /// <summary>Original filename as submitted by the HTTP client. Stored for display; not used to construct the storage path (which is built from the GUID to avoid collision and path traversal).</summary>
    public string Filename { get; private set; } = default!;

    /// <summary>File size in bytes. Validated against the 25 MB cap in the command handler before the stream is read; stored here for informational display.</summary>
    public long FileSize { get; private set; }

    /// <summary>MIME type reported by the HTTP client (e.g., "application/pdf"). Stored so the download handler can set the correct Content-Type response header.</summary>
    public string MimeType { get; private set; } = default!;

    /// <summary>
    /// Opaque path string used by IFileStorageService to locate the file in the
    /// storage backend. Application code treats this as a black box — the format is
    /// an implementation detail of the storage provider. In Level 1 this is a relative
    /// file path; in a cloud-storage level it would be a blob key or object URL.
    /// </summary>
    public string StoragePath { get; private set; } = default!;

    /// <summary>UTC timestamp of when the file was uploaded and the entity was created.</summary>
    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>
    /// Private parameterless constructor required by EF Core for materialisation.
    /// Application code must use Create() instead.
    /// </summary>
    private FileMetadata() { }

    /// <summary>
    /// Factory method: the only legitimate way for application code to create a FileMetadata.
    ///
    /// Why a static factory?
    ///   1. Enforces all invariants (GUID generation, UTC timestamp) in one place.
    ///   2. Application-generated Guid.NewGuid() matches ValueGeneratedNever() in EF
    ///      config — EF includes the Id in the INSERT statement rather than expecting
    ///      the database to generate it.
    ///   3. StoragePath is passed in from IFileStorageService.SaveAsync(), which runs
    ///      first inside the command handler — the entity is created only after the bytes
    ///      are safely on disk, preventing orphaned metadata records.
    /// </summary>
    public static FileMetadata Create(
        string userId,
        string filename,
        long fileSize,
        string mimeType,
        string storagePath)
    {
        return new FileMetadata
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Filename = filename,
            FileSize = fileSize,
            MimeType = mimeType,
            StoragePath = storagePath,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Ownership check: returns true if this file belongs to the given userId.
    ///
    /// Why a domain method instead of comparing UserId directly in the handler?
    ///   Centralising the ownership rule on the entity means: (a) the logic is in one
    ///   place, (b) it reads naturally at the call site ("file.BelongsTo(userId)"),
    ///   and (c) if the ownership model ever changes, there is exactly one place to update.
    ///   The delete handler calls this before allowing deletion; the download handler
    ///   does the same before streaming bytes — both get the same semantic check.
    /// </summary>
    public bool BelongsTo(string userId) => UserId == userId;
}
