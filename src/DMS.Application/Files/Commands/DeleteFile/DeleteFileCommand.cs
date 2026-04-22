using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Files.Commands.DeleteFile;

public sealed record DeleteFileCommand(Guid FileId, string UserId) : IRequest<Result>;
