using AreWeDoomd.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> post)
    {
        post.ToTable("Posts");

        post.HasKey(x => x.Id);

        post.Property(x => x.UserId).IsRequired();

        post.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(10_000);

        post.Property(x => x.LikeCount).IsRequired();
        post.Property(x => x.CommentCount).IsRequired();

        post.Property(x => x.CreatedAt).IsRequired();
        post.Property(x => x.UpdatedAt);

        // --- Comments: use the PROPERTY navigation, bind to backing field ---
        post.Navigation(x => x.Comments)
            .HasField("_comments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        post.HasMany(x => x.Comments)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // --- Likes: same pattern ---
        post.Navigation(x => x.Likes)
            .HasField("_likes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        post.HasMany(x => x.Likes)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
