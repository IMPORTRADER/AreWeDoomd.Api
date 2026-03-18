namespace AreWeDoomd.Domain.Users
{
    public sealed class UserFollow
    {
        public Guid Id { get; private set; }
        public Guid FollowerId { get; private set; }
        public Guid FollowingId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        private UserFollow() { } // EF Core

        private UserFollow(Guid id, Guid followerId, Guid followingId, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            }
            if (followerId == Guid.Empty)
            {
                throw new ArgumentException("FollowerId cannot be empty.", nameof(followerId));
            }
            if (followingId == Guid.Empty)
            {
                throw new ArgumentException("FollowingId cannot be empty.", nameof(followingId));
            }
            if (followerId == followingId)
            {
                throw new ArgumentException("A user cannot follow themselves.", nameof(followingId));
            }

            Id = id;
            FollowerId = followerId;
            FollowingId = followingId;
            CreatedAt = createdAt;
        }

        public static UserFollow Create(Guid followerId, Guid followingId, DateTimeOffset now)
            => new(Guid.NewGuid(), followerId, followingId, now);
    }
}
