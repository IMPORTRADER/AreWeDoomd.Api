using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Common;
using AreWeDoomd.Application.Features.Posts.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AreWeDoomd.IntegrationTests.AgentService;

/// <summary>
/// End-to-end contract test for <see cref="ContextFetcher"/> against the real API.
/// Unlike the unit test (which hand-writes the response JSON and therefore can only ever
/// agree with the wire DTO it was written next to), this test lets the real API serialize
/// the response. If the API's comment-list response shape drifts away from what
/// <see cref="ContextFetcher"/> expects, this test goes red.
/// </summary>
public sealed class ContextFetcherApiContractTests : IClassFixture<StubbedApiFactory>
{
    private readonly StubbedApiFactory _factory;

    public ContextFetcherApiContractTests(StubbedApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FetchAsync_ParsesCommentsFromRealApiResponse()
    {
        var postId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var commentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var postAuthor = new PostAuthorResult(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "doombot", "Ai", null);
        var commentAuthor = new PostAuthorResult(
            Guid.Parse("44444444-4444-4444-4444-444444444444"), "alice", "Human", null);

        _factory.PostResult = new PostResult(
            postId, postAuthor, "Is AGI near?", LikeCount: 0, CommentCount: 1,
            CreatedAt: new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), UpdatedAt: null);
        _factory.CommentListResult = new CommentListResult(
            Comments:
            [
                new CommentResult(
                    commentId, postId, commentAuthor, "Probably not.", LikeCount: 0,
                    CreatedAt: new DateTimeOffset(2026, 6, 10, 10, 5, 0, TimeSpan.Zero), UpdatedAt: null)
            ],
            TotalCount: 1,
            HasMoreBefore: false,
            HasMoreAfter: false);

        var fetcher = CreateFetcher();

        var context = await fetcher.FetchAsync(postId, postAuthor.UserId.ToString(), CancellationToken.None);

        context.ShouldNotBeNull();
        context!.Post.AuthorUsername.ShouldBe("doombot");
        context.Post.AuthorUserType.ShouldBe("Ai");
        context.Post.Content.ShouldBe("Is AGI near?");
        context.Comments.Count.ShouldBe(1);
        context.Comments[0].AuthorUsername.ShouldBe("alice");
        context.Comments[0].AuthorUserType.ShouldBe("Human");
        context.Comments[0].Content.ShouldBe("Probably not.");
    }

    private ContextFetcher CreateFetcher()
    {
        var httpClientFactory = new SingleClientHttpFactory(_factory.CreateClient());
        var options = Options.Create(new AgentServiceOptions { ApiBaseUrl = "http://localhost" });

        return new ContextFetcher(httpClientFactory, options, NullLogger<ContextFetcher>.Instance);
    }

    private sealed class SingleClientHttpFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }
}
