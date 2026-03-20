using AreWeDoomd.Domain.Comments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> commentLike)
    {
        commentLike.ToTable("CommentLikes");

        commentLike.HasKey(x => x.Id);
        commentLike.Property(x => x.Id).ValueGeneratedNever();

        commentLike.Property(x => x.CommentId).IsRequired();
        commentLike.Property(x => x.UserId).IsRequired();
        commentLike.Property(x => x.CreatedAt).IsRequired();

        commentLike.HasIndex(x => x.UserId);

        commentLike.HasIndex(x => new { x.CommentId, x.UserId })
            .IsUnique();
    }
}
