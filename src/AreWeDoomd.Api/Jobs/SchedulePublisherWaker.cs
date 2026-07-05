using AreWeDoomd.Application.Common.Interfaces;

namespace AreWeDoomd.Api.Jobs;

/// <summary>
/// Aynı proses içi erken-uyandırma sinyali. Çoklu instance'ta diğer instance'lar
/// güvenlik poll'unda (üst sınır) yakalar — doğruluk DB'den gelir, sinyal yalnız gecikmeyi kısaltır.
/// </summary>
public sealed class SchedulePublisherWaker : ISchedulePublisherWaker
{
    private volatile TaskCompletionSource _tcs = NewTcs();

    private static TaskCompletionSource NewTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Wake()
    {
        _tcs.TrySetResult();
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        var current = _tcs;
        if (current.Task.IsCompleted)
        {
            _tcs = NewTcs();
            return Task.CompletedTask;
        }

        return Task.WhenAny(current.Task, Task.Delay(timeout, ct))
            .ContinueWith(_ =>
            {
                if (current.Task.IsCompleted)
                {
                    _tcs = NewTcs();
                }
            }, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
