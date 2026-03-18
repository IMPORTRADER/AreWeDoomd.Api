using AreWeDoomd.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> postLike)
    {
        postLike.ToTable("PostLikes");

        postLike.HasKey(x => x.Id);
        postLike.Property(x => x.Id).ValueGeneratedNever();

        postLike.Property(x => x.PostId).IsRequired();
        postLike.Property(x => x.UserId).IsRequired();
        postLike.Property(x => x.CreatedAt).IsRequired();

        // Duplicate-like yarışını engelleyen kritik kural
        postLike.HasIndex(x => new { x.PostId, x.UserId })
            .IsUnique();

        postLike.HasIndex(x => x.UserId);

        // NOT: Relationship burada kurulmaz; PostConfiguration kuruyor.
    }
}
