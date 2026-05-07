using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Files.Commands.RestoreFileVersion;

public sealed record RestoreFileVersionCommand(
    Guid FileId,
    Guid VersionId,
    Guid UserId) : IRequest<Result>;
