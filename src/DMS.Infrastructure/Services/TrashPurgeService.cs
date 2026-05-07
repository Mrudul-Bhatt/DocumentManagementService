using DMS.Application.Services;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using DMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Services;

/// <summary>
/// Permanently deletes files and folders whose DeletedAt is older than the retention cutoff.
/// Injecting AppDbContext directly (instead of repository interfaces) is intentional:
/// purge queries span all users and require a cutoff filter not on any domain repository.
/// Adding such methods to IFileMetadataRepository or IFolderRepository would couple
/// those general-purpose interfaces to a single background-job concern.
/// </summary>
internal sealed class TrashPurgeService(
    AppDbContext dbContext,
    IFileVersionRepository versionRepository,
    IFileStorageService storageService) : ITrashPurgeService
{
    public async Task PurgeExpiredItemsAsync(int retentionDays, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var expiredFiles = await dbContext.FileMetadata
            .IgnoreQueryFilters()
            .Where(f => f.DeletedAt != null && f.DeletedAt < cutoff)
            .ToListAsync(ct);

        foreach (var file in expiredFiles)
        {
            var versions = await versionRepository.GetByFileIdAsync(file.Id, ct);

            foreach (var v in versions)
            {
                await storageService.DeleteAsync(v.StoragePath, ct);
                await versionRepository.DeleteAsync(v, ct);
            }

            // Pre-versioning files have no FileVersion rows — delete the physical file directly
            if (!versions.Any())
                await storageService.DeleteAsync(file.StoragePath, ct);

            dbContext.FileMetadata.Remove(file);
        }

        var expiredFolders = await dbContext.Folders
            .IgnoreQueryFilters()
            .Where(f => f.DeletedAt != null && f.DeletedAt < cutoff)
            .ToListAsync(ct);

        dbContext.Folders.RemoveRange(expiredFolders);

        await dbContext.SaveChangesAsync(ct);
    }
}
