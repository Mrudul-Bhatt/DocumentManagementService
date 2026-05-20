using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Enums;
using MediatR;

namespace DMS.Application.Shares.Queries.ListShares;

public sealed record ListSharesQuery(
    Guid ResourceId,
    ShareResourceType ResourceType,
    Guid RequestingUserId) : IRequest<Result<IReadOnlyList<ShareDto>>>;
