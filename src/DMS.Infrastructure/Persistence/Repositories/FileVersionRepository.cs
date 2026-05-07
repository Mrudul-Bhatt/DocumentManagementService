using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

internal sealed class FileVersionRepository(AppDbContext dbContext) : IFileVersionRepository
{
    public async Task<FileVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.FileVersions.FindAsync([id], ct);

    public async Task<IReadOnlyList<FileVersion>> GetByFileIdAsync(Guid fileId, CancellationToken ct = default) =>
        await dbContext.FileVersions
            .Where(v => v.FileId == fileId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

    public async Task<int> GetLatestVersionNumberAsync(Guid fileId, CancellationToken ct = default) =>
        await dbContext.FileVersions
            .Where(v => v.FileId == fileId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct) ?? 0;

    public async Task AddAsync(FileVersion version, CancellationToken ct = default)
    {
        await dbContext.FileVersions.AddAsync(version, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(FileVersion version, CancellationToken ct = default)
    {
        dbContext.FileVersions.Remove(version);
        await dbContext.SaveChangesAsync(ct);
    }
}
