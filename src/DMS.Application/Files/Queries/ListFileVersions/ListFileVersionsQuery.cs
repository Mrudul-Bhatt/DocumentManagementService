using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.ListFileVersions;

public sealed record ListFileVersionsQuery(
    Guid FileId,
    Guid UserId) : IRequest<Result<IReadOnlyList<FileVersionDto>>>;
