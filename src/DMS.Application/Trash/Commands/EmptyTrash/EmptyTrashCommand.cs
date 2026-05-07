using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Trash.Commands.EmptyTrash;

public sealed record EmptyTrashCommand(
    Guid UserId) : IRequest<Result>;
