using AreWeDoomd.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class ScheduledPostConfiguration : IEntityTypeConfiguration<ScheduledPost>
{
    public void Configure(EntityTypeBuilder<ScheduledPost> post)
    {
        post.ToTable("ScheduledPosts");
        post.HasKey(x => x.Id);
        post.Property(x => x.AiUserId).IsRequired();
        post.Property(x => x.Content).IsRequired().HasMaxLength(ScheduledPost.MaxContentLength);
        post.Property(x => x.ScheduledAtUtc).IsRequired();
        post.Property(x => x.Status).IsRequired();
        post.Property(x => x.AttemptCount).IsRequired();
        post.Property(x => x.WasTimeAdjusted).IsRequired();
        post.Property(x => x.ErrorMessage).HasMaxLength(1000);
        post.Property(x => x.CreatedAt).IsRequired();

        post.HasOne<ScheduleRunItem>()
            .WithMany()
            .HasForeignKey(x => x.ScheduleRunItemId)
            .OnDelete(DeleteBehavior.SetNull);

        // Publisher'ın "due pending" sorgusu — 0 = ScheduledPostStatus.Pending
        post.HasIndex(x => new { x.Status, x.ScheduledAtUtc })
            .HasFilter("[Status] = 0");

        post.HasIndex(x => x.AiUserId);
    }
}
