using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

internal sealed class ShareConfiguration : IEntityTypeConfiguration<Share>
{
    public void Configure(EntityTypeBuilder<Share> builder)
    {
        builder.ToTable("Shares");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.ResourceId)
            .IsRequired();

        builder.Property(s => s.ResourceType)
            .IsRequired();

        builder.Property(s => s.GrantedToUserId)
            .IsRequired();

        builder.Property(s => s.GrantedByUserId)
            .IsRequired();

        builder.Property(s => s.Role)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // One share per (resource, grantee) pair — prevents duplicate grants.
        builder.HasIndex(s => new { s.ResourceId, s.GrantedToUserId })
            .IsUnique()
            .HasDatabaseName("UX_Shares_ResourceId_GrantedToUserId");

        builder.HasIndex(s => s.GrantedToUserId)
            .HasDatabaseName("IX_Shares_GrantedToUserId");

        builder.HasIndex(s => s.ResourceId)
            .HasDatabaseName("IX_Shares_ResourceId");
    }
}
