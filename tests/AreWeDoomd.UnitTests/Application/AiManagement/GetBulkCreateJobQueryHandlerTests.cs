using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetBulkCreateJob;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetBulkCreateJobQueryHandlerTests
{
    private readonly Mock<IBulkCreateJobStore> _store = new();
    private readonly Mock<IAiUserReadRepository> _repo = new();

    private GetBulkCreateJobQueryHandler CreateHandler() =>
        new(_store.Object, _repo.Object);

    private static BulkCreateJobSnapshot MakeSnapshot(Guid jobId, string status = "completed") =>
        new(jobId, status, 3, 3, 3, [], ["a", "b", "c"],
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, Rebuilt: false);

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
        _repo.Verify(r => r.ListUsernamesByBulkJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStoreMiss_AndDbHasUsers_ReturnsRebuiltSnapshot()
    {
        var jobId = Guid.NewGuid();
        _store.Setup(s => s.TryGetSnapshot(jobId)).Returns((BulkCreateJobSnapshot?)null);
        _repo.Setup(r => r.ListUsernamesByBulkJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "alpha", "beta" });

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
    public async Task Handle_WhenStoreMiss_AndNoDbUsers_ReturnsNotFound()
    {
        var jobId = Guid.NewGuid();
        _store.Setup(s => s.TryGetSnapshot(jobId)).Returns((BulkCreateJobSnapshot?)null);
        _repo.Setup(r => r.ListUsernamesByBulkJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var result = await CreateHandler().Handle(new GetBulkCreateJobQuery(jobId), default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("bulk_job.not_found");
    }
}
