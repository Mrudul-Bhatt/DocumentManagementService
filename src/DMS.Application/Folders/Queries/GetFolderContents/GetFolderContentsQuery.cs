using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Folders.Queries.GetFolderContents;

public sealed record GetFolderContentsQuery(
    Guid? FolderId,
    Guid UserId) : IRequest<Result<FolderContentsDto>>;
