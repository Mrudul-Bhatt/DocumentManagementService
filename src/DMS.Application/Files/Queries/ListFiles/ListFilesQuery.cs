using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.ListFiles;

public sealed record ListFilesQuery(string UserId) : IRequest<Result<IReadOnlyList<FileMetadataDto>>>;
