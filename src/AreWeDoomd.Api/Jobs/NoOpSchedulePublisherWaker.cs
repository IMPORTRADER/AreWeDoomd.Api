using AreWeDoomd.Application.Common.Interfaces;

namespace AreWeDoomd.Api.Jobs;

/// <summary>
/// No-op waker used until the real SchedulePublisher hosted service is wired up.
/// </summary>
public sealed class NoOpSchedulePublisherWaker : ISchedulePublisherWaker
{
    public void Wake()
    {
    }
}
