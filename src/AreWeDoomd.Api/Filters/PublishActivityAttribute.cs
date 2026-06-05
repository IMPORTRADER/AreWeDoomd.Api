using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishActivityAttribute(
    ActivityType activityType,
    ActorType actorType,
    ActivityObjectType objectType,
    string? objectIdParam,
    ActivityTargetType targetType,
    string? targetIdParam) : Attribute
{
    public ActivityType ActivityType { get; } = activityType;
    public ActorType ActorType { get; } = actorType;
    public ActivityObjectType ObjectType { get; } = objectType;
    public string? ObjectIdParam { get; } = objectIdParam;
    public ActivityTargetType TargetType { get; } = targetType;
    public string? TargetIdParam { get; } = targetIdParam;
}
