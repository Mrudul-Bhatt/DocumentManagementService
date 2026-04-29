using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMS.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.UserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ResourceType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ResourceId)
            .HasMaxLength(255);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.OccurredAt)
            .IsRequired();

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditLogs_UserId");

        builder.HasIndex(a => a.OccurredAt)
            .HasDatabaseName("IX_AuditLogs_OccurredAt");
    }
}
