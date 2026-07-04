using System.Collections.Concurrent;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Api.Jobs;

public sealed class BulkCreateJobStore : IBulkCreateJobStore
{
    private readonly ConcurrentDictionary<Guid, BulkJobState> _jobs = new();

    public void Create(Guid jobId, int requestedCount)
    {
        _jobs[jobId] = new BulkJobState { JobId = jobId, Status = "queued", Requested = requestedCount };
    }

    public BulkCreateJobSnapshot? TryGetSnapshot(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return null;
        }

        lock (state)
        {
            return state.ToSnapshot();
        }
    }

    public void MarkStarted(Guid jobId, DateTimeOffset startedAt)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            lock (state)
            {
                state.Status = "generating";
                state.StartedAt = startedAt;
            }
        }
    }

    public void SetStatus(Guid jobId, string status)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            lock (state)
            {
                state.Status = status;
            }
        }
    }

    public void IncrementGenerated(Guid jobId, int count)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            lock (state)
            {
                state.Generated += count;
            }
        }
    }

    public void RecordCreated(Guid jobId, string username)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            lock (state)
            {
                state.Created++;
                state.CreatedUsers.Add(username);
            }
        }
    }

    public void RecordFailed(Guid jobId, string? username, string reason)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            lock (state)
            {
                state.Failed.Add(new BulkCreateFailedEntry(username, reason));
            }
        }
    }

    public void Complete(Guid jobId, DateTimeOffset finishedAt)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            lock (state)
            {
                state.Status = "completed";
                state.FinishedAt = finishedAt;
            }
        }
    }

    public int GetCreatedPlusFailed(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return 0;
        }

        lock (state)
        {
            return state.Created + state.Failed.Count;
        }
    }
}
