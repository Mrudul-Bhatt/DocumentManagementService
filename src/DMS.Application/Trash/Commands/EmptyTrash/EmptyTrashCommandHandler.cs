using DMS.Application.Common;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Trash.Commands.EmptyTrash;

internal sealed class EmptyTrashCommandHandler(
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository,
    IFileStorageService storageService,
    IFileVersionRepository versionRepository)
    : IRequestHandler<EmptyTrashCommand, Result>
{
    public async Task<Result> Handle(EmptyTrashCommand command, CancellationToken ct)
    {
        var deletedFiles   = await fileRepository.GetDeletedByUserIdAsync(command.UserId.ToString(), ct);
        var deletedFolders = await folderRepository.GetDeletedByUserIdAsync(command.UserId, ct);

        foreach (var file in deletedFiles)
        {
            // Hard-delete all versions' physical files then the version records
            var versions = await versionRepository.GetByFileIdAsync(file.Id, ct);
            foreach (var v in versions)
            {
                await storageService.DeleteAsync(v.StoragePath, ct);
                await versionRepository.DeleteAsync(v, ct);
            }

            // Delete the physical file if no version tracked the current path
            // (files uploaded before versioning was introduced)
            if (!versions.Any())
                await storageService.DeleteAsync(file.StoragePath, ct);

            await fileRepository.DeleteAsync(file, ct);
        }

        foreach (var folder in deletedFolders)
            await folderRepository.DeleteAsync(folder, ct);

        return Result.Success();
    }
}
