namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record ActivityObject(
    string Id,
    string Type,
    string? TextPreview);
