using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

internal sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.ToTable("FileMetadata");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        builder.Property(f => f.UserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.Filename)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.FileSize)
            .IsRequired();

        builder.Property(f => f.MimeType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.UploadedAt)
            .IsRequired();

        builder.HasIndex(f => f.UserId)
            .HasDatabaseName("IX_FileMetadata_UserId");
    }
}
