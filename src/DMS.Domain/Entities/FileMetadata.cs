namespace DMS.Domain.Entities;

public sealed class FileMetadata
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string Filename { get; private set; } = default!;
    public long FileSize { get; private set; }
    public string MimeType { get; private set; } = default!;
    public string StoragePath { get; private set; } = default!;
    public DateTimeOffset UploadedAt { get; private set; }

    private FileMetadata() { }

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

    public bool BelongsTo(string userId) => UserId == userId;
}
