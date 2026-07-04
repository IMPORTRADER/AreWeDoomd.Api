using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetSessionLog;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetSessionLogQueryHandlerTests
{
    private readonly Mock<ISessionLogReader> _reader = new();

    private GetSessionLogQueryHandler CreateHandler() => new(_reader.Object);

    [Fact]
    public async Task Handle_WhenReaderReturnsNull_ReturnsNotFound()
    {
        _reader
            .Setup(r => r.ReadAsync("2026-07-04/missing_log.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var query = new GetSessionLogQuery("2026-07-04/missing_log.txt");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorType.ShouldBe(ErrorType.NotFound);
        result.Error!.Code.ShouldBe("session_log.not_found");
    }

    [Fact]
    public async Task Handle_WhenReaderReturnsContent_ReturnsSuccess()
    {
        const string content = "PROMPT\n---\nRESPONSE";
        _reader
            .Setup(r => r.ReadAsync("2026-07-04/act-1_attempt1_123105.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var query = new GetSessionLogQuery("2026-07-04/act-1_attempt1_123105.txt");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Content.ShouldBe(content);
    }
}
