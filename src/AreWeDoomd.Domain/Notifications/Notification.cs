namespace AreWeDoomd.Domain.Notifications
{
    public sealed class Notification
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string ActivityId { get; private set; } = null!;
        public string ActivityType { get; private set; } = null!;
        public string ActorName { get; private set; } = null!;
        public string ActorType { get; private set; } = null!;
        public string Template { get; private set; } = null!;
        public string ParamsJson { get; private set; } = null!;
        public string DedupeKey { get; private set; } = null!;
        public DateTimeOffset CreatedAt { get; private set; }
        public bool IsRead { get; private set; }
        public DateTimeOffset? ReadAt { get; private set; }

        private Notification() { } // EF Core

        private Notification(
            Guid id,
            Guid userId,
            string activityId,
            string activityType,
            string actorName,
            string actorType,
            string template,
            string paramsJson,
            string dedupeKey,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            if (string.IsNullOrWhiteSpace(activityId))
                throw new ArgumentException("ActivityId is required.", nameof(activityId));
            if (string.IsNullOrWhiteSpace(activityType))
                throw new ArgumentException("ActivityType is required.", nameof(activityType));
            if (string.IsNullOrWhiteSpace(actorName))
                throw new ArgumentException("ActorName is required.", nameof(actorName));
            if (string.IsNullOrWhiteSpace(actorType))
                throw new ArgumentException("ActorType is required.", nameof(actorType));
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Template is required.", nameof(template));
            if (string.IsNullOrWhiteSpace(dedupeKey))
                throw new ArgumentException("DedupeKey is required.", nameof(dedupeKey));

            Id = id;
            UserId = userId;
            ActivityId = activityId;
            ActivityType = activityType;
            ActorName = actorName;
            ActorType = actorType;
            Template = template;
            ParamsJson = paramsJson ?? "{}";
            DedupeKey = dedupeKey;
            CreatedAt = createdAt;
            IsRead = false;
            ReadAt = null;
        }

        public static Notification Create(
            Guid userId,
            string activityId,
            string activityType,
            string actorName,
            string actorType,
            string template,
            string paramsJson,
            string dedupeKey,
            DateTimeOffset createdAt)
        {
            return new Notification(
                Guid.NewGuid(),
                userId,
                activityId,
                activityType,
                actorName,
                actorType,
                template,
                paramsJson,
                dedupeKey,
                createdAt);
        }

        public void MarkAsRead(DateTimeOffset readAt)
        {
            if (IsRead)
                return;

            IsRead = true;
            ReadAt = readAt;
        }
    }
}
