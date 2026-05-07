namespace DMS.Application.Services;

/// <summary>
/// Purges files and folders that have been in the Trash longer than the retention period.
/// Implemented in Infrastructure as a nightly BackgroundService.
/// </summary>
public interface ITrashPurgeService
{
    Task PurgeExpiredItemsAsync(int retentionDays, CancellationToken ct = default);
}
