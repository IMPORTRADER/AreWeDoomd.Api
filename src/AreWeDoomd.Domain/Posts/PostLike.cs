namespace AreWeDoomd.Domain.Posts
{
    public sealed class PostLike
    {
        public Guid Id { get; private set; }
        public Guid PostId { get; private set; }
        public Guid UserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        // Navigation (opsiyonel ama EF ergonomisi için yararlı)
        public Post Post { get; private set; } = null!;

        private PostLike() { } // EF Core

        private PostLike(Guid id, Guid postId, Guid userId, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            }
            if (postId == Guid.Empty)
            {
                throw new ArgumentException("PostId cannot be empty.", nameof(postId));
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            }

            Id = id;
            PostId = postId;
            UserId = userId;
            CreatedAt = createdAt;
        }

        public static PostLike Create(Guid postId, Guid userId, DateTimeOffset now)
            => new(Guid.NewGuid(), postId, userId, now);
    }
}
