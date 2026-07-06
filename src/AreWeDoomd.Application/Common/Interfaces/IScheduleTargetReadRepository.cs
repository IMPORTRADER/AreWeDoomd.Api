using AreWeDoomd.Application.Features.PostScheduling.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IScheduleTargetReadRepository
{
    Task<IReadOnlyList<ScheduleTarget>> GetTargetsAsync(IReadOnlyList<Guid>? aiUserIds, CancellationToken ct);
    Task<IReadOnlyList<AiUserSummary>> GetUserSummariesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
}
