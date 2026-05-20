using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

internal sealed class PublicLinkConfiguration : IEntityTypeConfiguration<PublicLink>
{
    public void Configure(EntityTypeBuilder<PublicLink> builder)
    {
        builder.ToTable("PublicLinks");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedNever();

        builder.Property(l => l.ResourceId)
            .IsRequired();

        builder.Property(l => l.ResourceType)
            .IsRequired();

        // 64-char hex token (32 random bytes). Unique — used as the URL path segment.
        builder.Property(l => l.Token)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(l => l.Token)
            .IsUnique()
            .HasDatabaseName("UX_PublicLinks_Token");

        builder.Property(l => l.Role)
            .IsRequired();

        builder.Property(l => l.CreatedByUserId)
            .IsRequired();

        builder.Property(l => l.ExpiresAt)
            .IsRequired(false);

        builder.Property(l => l.PasswordHash)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(l => l.CreatedAt)
            .IsRequired();

        builder.HasIndex(l => l.ResourceId)
            .HasDatabaseName("IX_PublicLinks_ResourceId");
    }
}
