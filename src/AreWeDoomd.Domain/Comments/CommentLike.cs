namespace AreWeDoomd.Domain.Comments
{
    public sealed class CommentLike
    {
        public Guid Id { get; private set; }
        public Guid CommentId { get; private set; }
        public Guid UserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public Comment Comment { get; private set; } = null!;

        private CommentLike() { } // EF Core

        private CommentLike(Guid id, Guid commentId, Guid userId, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            }
            if (commentId == Guid.Empty)
            {
                throw new ArgumentException("CommentId cannot be empty.", nameof(commentId));
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            }

            Id = id;
            CommentId = commentId;
            UserId = userId;
            CreatedAt = createdAt;
        }

        public static CommentLike Create(Guid commentId, Guid userId, DateTimeOffset now)
            => new(Guid.NewGuid(), commentId, userId, now);
    }
}
