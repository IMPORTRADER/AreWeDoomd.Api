using System.Net;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.UnitTests.AgentService.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Actions;

public sealed class ActionExecutorTests
{
    private static readonly Guid PostId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CommentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string ActingUserId = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public async Task ExecuteAsync_WhenReplyComment_ShouldPostCommentWithImpersonationHeaders()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var executor = CreateExecutor(async (request, _) =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });
        var decision = new AgentDecision(AgentAction.ReplyComment, "Hello there!", "friendly");

        await executor.ExecuteAsync(decision, PostId, CommentId, ActingUserId, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured!.Method.ShouldBe(HttpMethod.Post);
        captured.RequestUri!.AbsolutePath.ShouldBe($"/api/posts/{PostId}/comments");
        captured.Headers.GetValues("X-Agent-Secret").Single().ShouldBe("test-secret");
        captured.Headers.GetValues("X-Agent-User-Id").Single().ShouldBe(ActingUserId);
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("Hello there!");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLikeComment_ShouldPostToLikesEndpoint()
    {
        HttpRequestMessage? captured = null;
        var executor = CreateExecutor((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var decision = new AgentDecision(AgentAction.LikeComment, null, "agree");

        await executor.ExecuteAsync(decision, PostId, CommentId, ActingUserId, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured!.Method.ShouldBe(HttpMethod.Post);
        captured.RequestUri!.AbsolutePath.ShouldBe($"/api/posts/{PostId}/comments/{CommentId}/likes");
        captured.Headers.GetValues("X-Agent-User-Id").Single().ShouldBe(ActingUserId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIgnore_ShouldMakeNoHttpCall()
    {
        var called = false;
        var executor = CreateExecutor((_, _) =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var decision = new AgentDecision(AgentAction.Ignore, null, "nothing to add");

        await executor.ExecuteAsync(decision, PostId, CommentId, ActingUserId, CancellationToken.None);

        called.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiFails_ShouldNotThrow()
    {
        var executor = CreateExecutor((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{}")
            }));
        var decision = new AgentDecision(AgentAction.ReplyComment, "Hi", "test");

        // Must not throw — failures are logged and dropped.
        await executor.ExecuteAsync(decision, PostId, CommentId, ActingUserId, CancellationToken.None);
    }

    private static ActionExecutor CreateExecutor(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler(responder)));

        var options = Options.Create(new AgentServiceOptions
        {
            ApiBaseUrl = "http://localhost:5188",
            SharedSecret = "test-secret"
        });

        return new ActionExecutor(factory.Object, options, NullLogger<ActionExecutor>.Instance);
    }
}
