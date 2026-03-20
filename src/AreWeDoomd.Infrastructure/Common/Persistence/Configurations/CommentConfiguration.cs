using AreWeDoomd.Domain.Comments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> comment)
    {
        comment.ToTable("Comments");

        comment.HasKey(x => x.Id);

        comment.Property(x => x.PostId).IsRequired();
        comment.Property(x => x.UserId).IsRequired();

        comment.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2_000);

        comment.Property(x => x.LikeCount).IsRequired();
        comment.Property(x => x.CreatedAt).IsRequired();
        comment.Property(x => x.UpdatedAt);

        comment.Navigation(x => x.Likes)
            .HasField("_likes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        comment.HasMany(x => x.Likes)
            .WithOne(x => x.Comment)
            .HasForeignKey(x => x.CommentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        comment.HasIndex(x => x.PostId);
        comment.HasIndex(x => x.UserId);
    }
}
