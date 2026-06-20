using AreWeDoomd.Domain.Comments;

namespace AreWeDoomd.Domain.Posts
{
    public sealed class Post
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string Content { get; private set; } = null!;

        // Denormalized counters (transaction/concurrency ile güncellenmeli)
        public int LikeCount { get; private set; }
        public int CommentCount { get; private set; }
        public int CommentLikeCount { get; private set; }

        // EF Core backing fields
        private readonly List<PostLike> _likes = [];
        public IReadOnlyCollection<PostLike> Likes => _likes;
        private readonly List<Comment> _comments = [];
        public IReadOnlyCollection<Comment> Comments => _comments;

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        private Post() { } // EF Core

        private Post(Guid id, Guid userId, string content, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            }

            Id = id;
            UserId = userId;
            CreatedAt = createdAt;
            SetContent(content);
        }

        public static Post Create(Guid userId, string content, DateTimeOffset now)
            => new(Guid.NewGuid(), userId, content, now);

        public void UpdateContent(string content, DateTimeOffset now)
        {
            SetContent(content);
            UpdatedAt = now;
        }

        public bool IsLikedBy(Guid userId)
        {
            if (userId == Guid.Empty) return false;
            return _likes.Any(x => x.UserId == userId);
        }

        /// <summary>
        /// Idempotent: kullanıcı zaten like'ladıysa false döner, state değişmez.
        /// </summary>
        public bool Like(Guid userId, DateTimeOffset now)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            }

            if (IsLikedBy(userId))
            {
                return false;
            }

            _likes.Add(PostLike.Create(Id, userId, now));
            LikeCount++;
            return true;
        }

        /// <summary>
        /// Idempotent: kullanıcı like'lamadıysa false döner, state değişmez.
        /// </summary>
        public bool Unlike(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            }

            var like = _likes.FirstOrDefault(x => x.UserId == userId);
            if (like is null) return false;

            _likes.Remove(like);
            LikeCount = Math.Max(0, LikeCount - 1);
            return true;
        }

        public Comment AddComment(Guid userId, string content, DateTimeOffset now)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            }

            var comment = Comment.Create(Id, userId, content, now);
            _comments.Add(comment);
            CommentCount++;
            return comment;
        }

        public bool RemoveComment(Guid commentId)
        {
            if (commentId == Guid.Empty)
            {
                throw new ArgumentException("CommentId cannot be empty.", nameof(commentId));
            }

            var comment = _comments.FirstOrDefault(x => x.Id == commentId);
            if (comment is null) return false;

            _comments.Remove(comment);
            CommentCount = Math.Max(0, CommentCount - 1);
            CommentLikeCount = Math.Max(0, CommentLikeCount - comment.LikeCount);
            return true;
        }

        public void IncrementCommentLikeCount()
        {
            CommentLikeCount++;
        }

        public void DecrementCommentLikeCount()
        {
            CommentLikeCount = Math.Max(0, CommentLikeCount - 1);
        }

        private void SetContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content is required.", nameof(content));
            }

            content = content.Trim();

            if (content.Length > 10_000)
            {
                throw new ArgumentOutOfRangeException(nameof(content), "Content is too long.");
            }

            Content = content;
        }
    }
}
