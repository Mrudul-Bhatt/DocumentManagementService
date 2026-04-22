using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.DownloadFile;

public sealed record DownloadFileQuery(Guid FileId, string UserId) : IRequest<Result<FileDownloadResult>>;
