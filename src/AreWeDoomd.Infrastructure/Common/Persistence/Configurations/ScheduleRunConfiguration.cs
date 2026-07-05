using AreWeDoomd.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class ScheduleRunConfiguration : IEntityTypeConfiguration<ScheduleRun>
{
    public void Configure(EntityTypeBuilder<ScheduleRun> run)
    {
        run.ToTable("ScheduleRuns");
        run.HasKey(x => x.Id);
        run.Property(x => x.RunDate).IsRequired();
        run.Property(x => x.TriggeredByUserId).IsRequired();
        run.Property(x => x.ThresholdSnapshot).IsRequired();
        run.Property(x => x.MaxPostsSnapshot).IsRequired();
        run.Property(x => x.PostLengthGuideSnapshot).IsRequired();
        run.Property(x => x.StrategySnapshot).IsRequired();
        run.Property(x => x.Status).IsRequired();
        run.Property(x => x.CreatedAt).IsRequired();

        run.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        run.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.ScheduleRunId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        run.HasIndex(x => x.RunDate);
    }
}
