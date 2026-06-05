namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record ActivityObject(
    string Id,
    ActivityObjectType Type,
    string? TextPreview);
