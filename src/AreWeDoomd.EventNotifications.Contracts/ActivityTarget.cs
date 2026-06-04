namespace AreWeDoomd.EventNotifications.Contracts;

public sealed record ActivityTarget(
    string Id,
    string Type,
    string OwnerId);
