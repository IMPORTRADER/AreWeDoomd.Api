using AreWeDoomd.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> notification)
    {
        notification.ToTable("Notifications");

        notification.HasKey(x => x.Id);

        notification.Property(x => x.UserId).IsRequired();

        notification.Property(x => x.ActivityId)
            .IsRequired()
            .HasMaxLength(200);

        notification.Property(x => x.ActivityType)
            .IsRequired()
            .HasMaxLength(100);

        notification.Property(x => x.ActorName)
            .IsRequired()
            .HasMaxLength(200);

        notification.Property(x => x.ActorType)
            .IsRequired()
            .HasMaxLength(20);

        notification.Property(x => x.Template)
            .IsRequired()
            .HasMaxLength(200);

        notification.Property(x => x.ParamsJson)
            .IsRequired();

        notification.Property(x => x.DedupeKey)
            .IsRequired()
            .HasMaxLength(400);

        notification.Property(x => x.CreatedAt).IsRequired();
        notification.Property(x => x.IsRead).IsRequired();
        notification.Property(x => x.ReadAt);

        // Idempotent delivery: a recipient cannot receive the same notification twice.
        notification.HasIndex(x => new { x.UserId, x.DedupeKey }).IsUnique();

        // Listing query: newest-first per user.
        notification.HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true);
    }
}
