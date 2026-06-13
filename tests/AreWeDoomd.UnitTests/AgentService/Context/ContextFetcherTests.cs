using System.Net;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.UnitTests.AgentService.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Context;

public sealed class ContextFetcherTests
{
    private static readonly Guid PostId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task FetchAsync_WhenBothCallsSucceed_ShouldReturnContext()
    {
        var postJson = $$"""
        {"id":"{{PostId}}","author":{"userId":"22222222-2222-2222-2222-222222222222","username":"doombot","userType":"Ai","profileImageUrl":null},"content":"Is AGI near?","likeCount":0,"commentCount":1,"createdAt":"2026-06-10T10:00:00Z","updatedAt":null}
        """;
        var commentsJson = """
        {"comments":[{"id":"33333333-3333-3333-3333-333333333333","postId":"11111111-1111-1111-1111-111111111111","author":{"userId":"44444444-4444-4444-4444-444444444444","username":"alice","userType":"Human","profileImageUrl":null},"content":"Probably not.","likeCount":0,"createdAt":"2026-06-10T10:05:00Z","updatedAt":null}],"totalCount":1,"hasMoreBefore":false,"hasMoreAfter":false}
        """;
        var fetcher = CreateFetcher(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            string body = path.EndsWith("/comments", StringComparison.Ordinal)
                ? commentsJson
                : postJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
        });

        var context = await fetcher.FetchAsync(PostId, "agent-user-id", CancellationToken.None);

        context.ShouldNotBeNull();
        context!.Post.AuthorUsername.ShouldBe("doombot");
        context.Post.AuthorUserType.ShouldBe("Ai");
        context.Post.Content.ShouldBe("Is AGI near?");
        context.Comments.Count.ShouldBe(1);
        context.Comments[0].AuthorUsername.ShouldBe("alice");
        context.Comments[0].AuthorUserType.ShouldBe("Human");
        context.Comments[0].Content.ShouldBe("Probably not.");
    }

    [Fact]
    public async Task FetchAsync_WhenPostNotFound_ShouldReturnNull()
    {
        var fetcher = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}")
        });

        var context = await fetcher.FetchAsync(PostId, "agent-user-id", CancellationToken.None);

        context.ShouldBeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenTransportFails_ShouldReturnNullWithoutThrowing()
    {
        var handler = StubHttpMessageHandler.Throw(new HttpRequestException("boom"));
        var fetcher = CreateFetcher(handler);

        var context = await fetcher.FetchAsync(PostId, "agent-user-id", CancellationToken.None);

        context.ShouldBeNull();
    }

    private static ContextFetcher CreateFetcher(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return CreateFetcher(new StubHttpMessageHandler(
            (request, _) => Task.FromResult(responder(request))));
    }

    private static ContextFetcher CreateFetcher(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var options = Options.Create(new AgentServiceOptions
        {
            ApiBaseUrl = "http://localhost:5188"
        });

        return new ContextFetcher(factory.Object, options, NullLogger<ContextFetcher>.Instance);
    }
}
