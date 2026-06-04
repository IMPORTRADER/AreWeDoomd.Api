namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record ActivityActor(
    string Id,
    ActorType Type,
    string DisplayName);
