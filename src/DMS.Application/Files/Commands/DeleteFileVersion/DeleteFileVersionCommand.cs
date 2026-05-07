using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Files.Commands.DeleteFileVersion;

public sealed record DeleteFileVersionCommand(
    Guid FileId,
    Guid VersionId,
    Guid UserId) : IRequest<Result>;
