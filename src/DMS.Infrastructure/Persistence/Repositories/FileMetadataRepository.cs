using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

internal sealed class FileMetadataRepository(AppDbContext dbContext) : IFileMetadataRepository
{
    public async Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.FileMetadata.FindAsync([id], ct);

    public async Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await dbContext.FileMetadata
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(ct);

    public async Task AddAsync(FileMetadata file, CancellationToken ct = default)
    {
        await dbContext.FileMetadata.AddAsync(file, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(FileMetadata file, CancellationToken ct = default)
    {
        dbContext.FileMetadata.Remove(file);
        await dbContext.SaveChangesAsync(ct);
    }
}
