using AreWeDoomd.ActivityNotifications.Contracts;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AreWeDoomd.Api.Filters;

/// <summary>
/// Bu attribute'ün konduğu endpoint'te, başarılı bir yanıtın ardından bir aktivite
/// bildirimi yayınlanır. Attribute aynı zamanda bir <see cref="IFilterFactory"/>'dir:
/// davranışı tetikleyen <see cref="ActivityPublishingFilter"/>'ı kendisi oluşturur, bu yüzden
/// global bir filter kaydına gerek yoktur ve davranış doğrudan endpoint'te görünür.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishActivityAttribute(
    ActivityType activityType,
    ActivityTargetType targetType,
    string? targetIdParam) : Attribute, IFilterFactory
{
    public ActivityType ActivityType { get; } = activityType;
    public ActivityTargetType TargetType { get; } = targetType;
    public string? TargetIdParam { get; } = targetIdParam;

    // Filter scoped servislere (notification engine vb.) bağlı; her istek için yeni instance gerekir.
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        ActivatorUtilities.CreateInstance<ActivityPublishingFilter>(serviceProvider, this);
}
