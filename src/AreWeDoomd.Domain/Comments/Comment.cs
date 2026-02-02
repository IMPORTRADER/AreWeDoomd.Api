using AreWeDoomd.Domain.Posts;

namespace AreWeDoomd.Domain.Comments
{
    public sealed class Comment
    {
        public Guid Id { get; private set; }
        public Guid PostId { get; private set; }
        public Guid UserId { get; private set; }
        public string Content { get; private set; } = null!;
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }
        public Post Post { get; private set; } = null!;

        private Comment() { } // EF Core

        private Comment(Guid id, Guid postId, Guid userId, string content, DateTimeOffset createdAt)
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
            SetContent(content);
        }

        public static Comment Create(Guid postId, Guid userId, string content, DateTimeOffset now)
            => new(Guid.NewGuid(), postId, userId, content, now);

        public void UpdateContent(string content, DateTimeOffset now)
        {
            SetContent(content);
            UpdatedAt = now;
        }

        private void SetContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content is required.", nameof(content));
            }

            content = content.Trim();

            if (content.Length > 2_000)
            {
                throw new ArgumentOutOfRangeException(nameof(content), "Comment is too long.");
            }

            Content = content;
        }
    }
}
