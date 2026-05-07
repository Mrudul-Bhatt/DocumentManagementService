namespace DMS.Domain.Entities;

public sealed class FileVersion
{
    public Guid Id { get; private set; }
    public Guid FileId { get; private set; }
    public int VersionNumber { get; private set; }
    public string StoragePath { get; private set; } = default!;
    public long FileSize { get; private set; }
    public string UploadedBy { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private FileVersion() { }

    public static FileVersion Create(
        Guid fileId,
        int versionNumber,
        string storagePath,
        long fileSize,
        string uploadedBy)
    {
        return new FileVersion
        {
            Id            = Guid.NewGuid(),
            FileId        = fileId,
            VersionNumber = versionNumber,
            StoragePath   = storagePath,
            FileSize      = fileSize,
            UploadedBy    = uploadedBy,
            CreatedAt     = DateTimeOffset.UtcNow
        };
    }
}
