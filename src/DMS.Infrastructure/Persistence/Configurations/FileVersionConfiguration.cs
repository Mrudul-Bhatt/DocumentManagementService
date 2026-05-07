using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

internal sealed class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.ToTable("FileVersions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .ValueGeneratedNever();

        builder.Property(v => v.FileId)
            .IsRequired();

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(v => v.FileSize)
            .IsRequired();

        builder.Property(v => v.UploadedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        // Unique constraint: only one record per (FileId, VersionNumber) pair.
        builder.HasIndex(v => new { v.FileId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_FileVersions_FileId_VersionNumber");

        builder.HasIndex(v => v.FileId)
            .HasDatabaseName("IX_FileVersions_FileId");
    }
}
