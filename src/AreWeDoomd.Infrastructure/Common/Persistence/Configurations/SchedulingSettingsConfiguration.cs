using AreWeDoomd.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class SchedulingSettingsConfiguration : IEntityTypeConfiguration<SchedulingSettings>
{
    public void Configure(EntityTypeBuilder<SchedulingSettings> settings)
    {
        settings.ToTable("SchedulingSettings");
        settings.HasKey(x => x.Id);
        settings.Property(x => x.DesireThreshold).IsRequired();
        settings.Property(x => x.MaxPostsPerDay).IsRequired();
        settings.Property(x => x.PostLengthGuide).IsRequired();
        settings.Property(x => x.LatePolicy).IsRequired();
        settings.Property(x => x.LateGraceHours).IsRequired();
        settings.Property(x => x.Strategy).IsRequired();
        settings.Property(x => x.UpdatedAt).IsRequired();
    }
}
