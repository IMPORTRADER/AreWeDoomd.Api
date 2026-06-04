using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Application.Notifications.Engine;

public sealed record ActivityContext(
    ActivityType ActivityType,
    string ActorId,
    ActorType ActorType,
    string ActorDisplayName,
    string ObjectId,
    ActivityObjectType ObjectType,
    string? ObjectTextPreview,
    string TargetId,
    ActivityTargetType TargetType,
    string TargetOwnerId,
    DateTimeOffset OccurredAt);
