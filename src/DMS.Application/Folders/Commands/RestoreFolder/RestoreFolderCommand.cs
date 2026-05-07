using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Folders.Commands.RestoreFolder;

public sealed record RestoreFolderCommand(
    Guid FolderId,
    Guid UserId) : IRequest<Result>;
