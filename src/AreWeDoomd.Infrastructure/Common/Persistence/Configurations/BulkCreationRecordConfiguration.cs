using AreWeDoomd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class BulkCreationRecordConfiguration : IEntityTypeConfiguration<BulkCreationRecord>
{
    public void Configure(EntityTypeBuilder<BulkCreationRecord> builder)
    {
        builder.ToTable("BulkCreationRecords");

        builder.HasKey(x => new { x.JobId, x.UserId });

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.JobId);
    }
}
