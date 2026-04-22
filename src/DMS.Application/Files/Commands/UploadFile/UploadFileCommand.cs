using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Commands.UploadFile;

public sealed record UploadFileCommand(
    string UserId,
    string Filename,
    string MimeType,
    long FileSize,
    Stream Content) : IRequest<Result<FileMetadataDto>>;
