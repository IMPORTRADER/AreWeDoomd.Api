using AreWeDoomd.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class ScheduleRunItemConfiguration : IEntityTypeConfiguration<ScheduleRunItem>
{
    public void Configure(EntityTypeBuilder<ScheduleRunItem> item)
    {
        item.ToTable("ScheduleRunItems");
        item.HasKey(x => x.Id);
        item.Property(x => x.AiUserId).IsRequired();
        item.Property(x => x.RunDate).IsRequired();
        item.Property(x => x.Status).IsRequired();
        item.Property(x => x.Reasoning).HasMaxLength(1000);
        item.Property(x => x.ModelUsed).HasMaxLength(200);
        item.Property(x => x.ErrorDetail).HasMaxLength(1000);
        item.Property(x => x.PushCount).IsRequired();
        item.Property(x => x.LastPushedAtUtc).IsRequired();
        item.Property(x => x.CreatedAt).IsRequired();

        // Aynı gün + aynı hesap için tek aktif item — çift tıklama/iki sekme yarışını
        // DB seviyesinde engeller. 4 = ScheduleRunItemStatus.Superseded (enum değeri sabit).
        item.HasIndex(x => new { x.AiUserId, x.RunDate })
            .IsUnique()
            .HasFilter("[Status] <> 4");

        // Sweep sorgusu için
        item.HasIndex(x => new { x.Status, x.LastPushedAtUtc });
    }
}
