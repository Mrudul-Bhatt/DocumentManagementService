using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API mapping configuration for the FileMetadata entity.
///
/// Why a separate IEntityTypeConfiguration<T> class instead of configuring
/// everything inside AppDbContext.OnModelCreating()?
///   Single Responsibility: each entity's mapping is self-contained and independently
///   readable. As the model grows (more entities, more complex mappings), OnModelCreating
///   stays flat — it delegates to these classes via ApplyConfigurationsFromAssembly().
///   A monolithic OnModelCreating that configures every entity in one method becomes
///   difficult to navigate and review quickly.
///
/// Why internal sealed?
///   This class is an infrastructure implementation detail. Nothing outside this
///   assembly needs to reference it — EF Core discovers and instantiates it
///   automatically via reflection during ApplyConfigurationsFromAssembly().
///   sealed prevents unintended subclassing.
/// </summary>
internal sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    /// <summary>
    /// Called once by EF Core during model building to apply all column, constraint,
    /// and index mappings for the FileMetadata entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        // Explicit table name avoids EF Core's default pluralisation convention
        // ("FileMetadatas"), which would produce an unintuitive table name.
        builder.ToTable("FileMetadata");

        builder.HasKey(f => f.Id);

        // ValueGeneratedNever() tells EF Core this column is NOT an identity/auto-increment —
        // the application generates the GUID via Guid.NewGuid() in FileMetadata.Create().
        // Without this, EF Core would assume the database generates the Id on INSERT
        // and would not include it in the INSERT statement, causing a runtime error.
        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        // MaxLength maps to nvarchar(255) in SQL Server.
        // 255 characters is sufficient for any realistic user identifier (email, GUID, sub claim).
        builder.Property(f => f.UserId)
            .HasMaxLength(255)
            .IsRequired();

        // 255 characters covers the maximum filename length enforced by most operating systems
        // and filesystems (Windows NTFS: 255, Linux ext4: 255). Longer names are rejected
        // at the OS level before they could ever reach this column.
        builder.Property(f => f.Filename)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.FileSize)
            .IsRequired();

        // MIME types follow the pattern "type/subtype[; parameter]".
        // The longest registered MIME types are well under 100 characters.
        builder.Property(f => f.MimeType)
            .HasMaxLength(100)
            .IsRequired();

        // 500 characters accommodates deeply nested paths and future storage backends
        // (e.g., Azure Blob URLs or S3 keys with bucket prefix and path segments).
        builder.Property(f => f.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.UploadedAt)
            .IsRequired();

        // Index on UserId supports GetByUserIdAsync — the most frequent query pattern.
        // Without this index, listing a user's files requires a full table scan,
        // which degrades linearly as the total number of records grows.
        // Explicit database name follows the convention IX_{Table}_{Column} for
        // consistent, predictable index naming across migrations.
        builder.HasIndex(f => f.UserId)
            .HasDatabaseName("IX_FileMetadata_UserId");
    }
}
