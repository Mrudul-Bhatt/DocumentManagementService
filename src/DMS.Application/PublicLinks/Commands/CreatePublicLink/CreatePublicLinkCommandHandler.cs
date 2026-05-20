using System.Security.Cryptography;
using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Application.Services;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.PublicLinks.Commands.CreatePublicLink;

internal sealed class CreatePublicLinkCommandHandler(
    IPublicLinkRepository linkRepository,
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository,
    IPasswordHasher passwordHasher) : IRequestHandler<CreatePublicLinkCommand, Result<PublicLinkDto>>
{
    public async Task<Result<PublicLinkDto>> Handle(CreatePublicLinkCommand command, CancellationToken ct)
    {
        // Verify caller owns the resource
        if (command.ResourceType == Domain.Enums.ShareResourceType.File)
        {
            var file = await fileRepository.GetByIdAsync(command.ResourceId, ct);
            if (file is null || !file.BelongsTo(command.CreatedByUserId.ToString()))
                return Result.Failure<PublicLinkDto>(DomainErrors.File.NotFound);
        }
        else
        {
            var folder = await folderRepository.GetByIdAsync(command.ResourceId, ct);
            if (folder is null || !folder.BelongsTo(command.CreatedByUserId))
                return Result.Failure<PublicLinkDto>(DomainErrors.Folder.NotFound);
        }

        // Generate a cryptographically random 32-byte token encoded as a 64-char hex string
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        var passwordHash = command.Password is not null
            ? passwordHasher.Hash(command.Password)
            : null;

        var link = PublicLink.Create(
            command.ResourceId,
            command.ResourceType,
            token,
            command.Role,
            command.CreatedByUserId,
            command.ExpiresAt,
            passwordHash);

        await linkRepository.AddAsync(link, ct);

        return Result.Success(new PublicLinkDto(
            link.Id,
            link.ResourceId,
            link.ResourceType,
            link.Token,
            link.Role,
            link.ExpiresAt,
            link.RequiresPassword(),
            link.CreatedAt));
    }
}
