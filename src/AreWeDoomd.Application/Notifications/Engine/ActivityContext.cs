namespace AreWeDoomd.Application.Notifications.Engine;

public sealed record ActivityContext(
    string ActivityType,
    string ActorId,
    string ActorType,
    string ActorDisplayName,
    string ObjectId,
    string ObjectType,
    string? ObjectTextPreview,
    string TargetId,
    string TargetType,
    string TargetOwnerId,
    DateTimeOffset OccurredAt);
