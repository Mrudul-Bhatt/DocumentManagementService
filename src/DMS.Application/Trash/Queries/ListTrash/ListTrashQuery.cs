using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Trash.Queries.ListTrash;

public sealed record ListTrashQuery(
    Guid UserId) : IRequest<Result<IReadOnlyList<TrashedItemDto>>>;
