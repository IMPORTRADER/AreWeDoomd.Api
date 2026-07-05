namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record StartScheduleRunRequest(List<Guid>? AiUserIds, bool OverwriteExisting);
