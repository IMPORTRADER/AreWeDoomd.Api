namespace AreWeDoomd.Application.Notifications.Dispatching;

public sealed record UserNotificationDto(
    Guid Id,
    string Template,
    Dictionary<string, string> Params,
    string ActorName,
    string ActorType,
    DateTimeOffset CreatedAt,
    bool IsRead);
