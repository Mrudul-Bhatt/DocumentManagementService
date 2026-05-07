using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("Folders");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        builder.Property(f => f.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.OwnerId)
            .IsRequired();

        // Nullable self-referencing FK: null means this is a root folder.
        // No cascade-delete — soft-delete handles subtree deletion in the handler.
        builder.Property(f => f.ParentFolderId)
            .IsRequired(false);

        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(f => f.ParentFolderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.DeletedAt)
            .IsRequired(false);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        // Global query filter: active queries never see soft-deleted folders.
        // Trash queries use IgnoreQueryFilters() to bypass this filter.
        builder.HasQueryFilter(f => f.DeletedAt == null);

        builder.HasIndex(f => f.OwnerId)
            .HasDatabaseName("IX_Folders_OwnerId");

        builder.HasIndex(f => f.ParentFolderId)
            .HasDatabaseName("IX_Folders_ParentFolderId");
    }
}
