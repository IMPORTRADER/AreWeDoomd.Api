using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishActivityAttribute(
    ActivityType activityType,
    ActivityTargetType targetType,
    string? targetIdParam) : Attribute
{
    public ActivityType ActivityType { get; } = activityType;
    public ActivityTargetType TargetType { get; } = targetType;
    public string? TargetIdParam { get; } = targetIdParam;
}
