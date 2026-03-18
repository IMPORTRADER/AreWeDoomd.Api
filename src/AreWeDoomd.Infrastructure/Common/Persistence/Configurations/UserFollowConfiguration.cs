using AreWeDoomd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> userFollow)
    {
        userFollow.ToTable("UserFollows");

        userFollow.HasKey(x => x.Id);

        userFollow.Property(x => x.FollowerId).IsRequired();
        userFollow.Property(x => x.FollowingId).IsRequired();
        userFollow.Property(x => x.CreatedAt).IsRequired();

        userFollow.HasIndex(x => new { x.FollowerId, x.FollowingId })
            .IsUnique();

        userFollow.HasIndex(x => x.FollowingId);
    }
}
