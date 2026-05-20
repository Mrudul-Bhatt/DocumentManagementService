using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Application.Services;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.PublicLinks.Queries.ResolvePublicLink;

internal sealed class ResolvePublicLinkQueryHandler(
    IPublicLinkRepository linkRepository,
    IPasswordHasher passwordHasher) : IRequestHandler<ResolvePublicLinkQuery, Result<PublicLinkDto>>
{
    public async Task<Result<PublicLinkDto>> Handle(ResolvePublicLinkQuery query, CancellationToken ct)
    {
        var link = await linkRepository.GetByTokenAsync(query.Token, ct);

        if (link is null)
            return Result.Failure<PublicLinkDto>(DomainErrors.PublicLink.NotFound);

        if (link.IsExpired())
            return Result.Failure<PublicLinkDto>(DomainErrors.PublicLink.Expired);

        if (link.RequiresPassword())
        {
            if (query.Password is null || !passwordHasher.Verify(query.Password, link.PasswordHash!))
                return Result.Failure<PublicLinkDto>(DomainErrors.PublicLink.InvalidPassword);
        }

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
