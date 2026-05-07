using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Folders.Commands.MoveFolder;

public sealed record MoveFolderCommand(
    Guid FolderId,
    Guid? TargetParentFolderId,
    Guid UserId) : IRequest<Result>;
