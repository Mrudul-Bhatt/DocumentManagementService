using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Shares.Queries.ListSharedWithMe;

public sealed record ListSharedWithMeQuery(
    Guid UserId) : IRequest<Result<IReadOnlyList<SharedResourceDto>>>;
