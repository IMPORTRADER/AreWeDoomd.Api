using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetBulkCreateJob;
using AreWeDoomd.Domain.Users;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetBulkCreateJobQueryHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T10:00:00Z");

    private readonly Mock<IBulkCreateJobStore> _store = new();
    private readonly Mock<IBulkCreationRecordRepository> _recordRepo = new();

    private GetBulkCreateJobQueryHandler CreateHandler() =>
        new(_store.Object, _recordRepo.Object);

    private static BulkCreateJobSnapshot MakeSnapshot(Guid jobId, string status = "completed") =>
        new(jobId, status, 3, 3, 3, [], ["a", "b", "c"],
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, Rebuilt: false);

    private static BulkCreationRecord MakeRecord(Guid jobId, string username) =>
        BulkCreationRecord.Create(jobId, Guid.NewGuid(), username, Now);

    [Fact]
    public async Task Handle_WhenStoreHit_ReturnsSnapshot()
    {
        var jobId = Guid.NewGuid();
        var snap = MakeSnapshot(jobId);
        _store.Setup(s => s.TryGetSnapshot(jobId)).Returns(snap);

        var result = await CreateHandler().Handle(new GetBulkCreateJobQuery(jobId), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.JobId.ShouldBe(jobId);
        result.Value.Status.ShouldBe("completed");
        result.Value.Rebuilt.ShouldBeFalse();
        _recordRepo.Verify(r => r.ListByJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStoreMiss_AndDbHasRecords_ReturnsRebuiltSnapshot()
    {
        var jobId = Guid.NewGuid();
        _store.Setup(s => s.TryGetSnapshot(jobId)).Returns((BulkCreateJobSnapshot?)null);
        _recordRepo.Setup(r => r.ListByJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BulkCreationRecord>
            {
                MakeRecord(jobId, "alpha"),
                MakeRecord(jobId, "beta")
            });

        var result = await CreateHandler().Handle(new GetBulkCreateJobQuery(jobId), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Rebuilt.ShouldBeTrue();
        result.Value.Status.ShouldBe("completed");
        result.Value.CreatedUsers.ShouldContain("alpha");
        result.Value.CreatedUsers.ShouldContain("beta");
        result.Value.Created.ShouldBe(2);
        result.Value.Failed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenStoreMiss_AndNoDbRecords_ReturnsNotFound()
    {
        var jobId = Guid.NewGuid();
        _store.Setup(s => s.TryGetSnapshot(jobId)).Returns((BulkCreateJobSnapshot?)null);
        _recordRepo.Setup(r => r.ListByJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BulkCreationRecord>());

        var result = await CreateHandler().Handle(new GetBulkCreateJobQuery(jobId), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("bulk_job.not_found");
    }
}
