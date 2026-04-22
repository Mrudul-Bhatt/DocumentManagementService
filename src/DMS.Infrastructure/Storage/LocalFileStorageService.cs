using DMS.Domain.Services;
using Microsoft.Extensions.Configuration;

namespace DMS.Infrastructure.Storage;

internal sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private string UploadRoot => configuration["Storage:UploadRoot"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public async Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default)
    {
        var directory = Path.Combine(UploadRoot, userId);
        Directory.CreateDirectory(directory);

        var storagePath = Path.Combine(directory, fileId);

        await using var fileStream = new FileStream(storagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        return storagePath;
    }

    public Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default)
    {
        if (!File.Exists(storagePath))
            throw new FileNotFoundException("File not found on storage.", storagePath);

        Stream stream = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (File.Exists(storagePath))
            File.Delete(storagePath);

        return Task.CompletedTask;
    }
}
