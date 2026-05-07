using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Trash.Commands.RestoreFromTrash;

public sealed record RestoreFromTrashCommand(
    Guid ItemId,
    TrashedItemType ItemType,
    Guid UserId) : IRequest<Result>;
