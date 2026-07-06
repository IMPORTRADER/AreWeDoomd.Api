using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;
using AreWeDoomd.Application.Features.Posts.Common;
using AreWeDoomd.Application.Features.Posts.Queries.GetPost;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AreWeDoomd.IntegrationTests.AgentService;

/// <summary>
/// Spins up the real API in-memory but replaces the MediatR query handlers behind the
/// posts/comments endpoints with stubs, so the test exercises the real controllers and
/// the real JSON serializer without needing a database. The whole point is that the
/// response JSON is produced by the API itself — not hand-written in the test — so a
/// change to the API's response shape is caught by anything that deserializes it.
/// </summary>
public sealed class StubbedApiFactory : WebApplicationFactory<Program>
{
    public PostResult? PostResult { get; set; }

    public CommentListResult? CommentListResult { get; set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((_, loggerConfig) =>
            loggerConfig.WriteTo.Console());

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // A dummy connection string keeps startup happy; no test path actually hits the DB.
        builder.UseSetting(
            "ConnectionStrings:AreWeDoomdSql",
            "Server=localhost;Database=test;Trusted_Connection=True;TrustServerCertificate=True;");
        // neutralize any host-machine Admin:Usernames so the startup seeder stays inert against the fake test DB
        builder.UseSetting("Admin:Usernames:0", "");
        builder.UseSetting("Admin:SeedOnStartup", "false");
        builder.UseSetting("Preflight:Database:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestHandler<GetPostQuery, Result<PostResult>>>();
            services.AddTransient<IRequestHandler<GetPostQuery, Result<PostResult>>>(
                _ => new StubGetPostHandler(this));

            services.RemoveAll<IRequestHandler<GetPostCommentsQuery, Result<CommentListResult>>>();
            services.AddTransient<IRequestHandler<GetPostCommentsQuery, Result<CommentListResult>>>(
                _ => new StubGetPostCommentsHandler(this));
        });
    }

    private sealed class StubGetPostHandler(StubbedApiFactory factory)
        : IRequestHandler<GetPostQuery, Result<PostResult>>
    {
        public Task<Result<PostResult>> Handle(GetPostQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(factory.PostResult is { } post
                ? Result<PostResult>.Success(post)
                : Result<PostResult>.NotFound("post.not_found", "Post not found."));
        }
    }

    private sealed class StubGetPostCommentsHandler(StubbedApiFactory factory)
        : IRequestHandler<GetPostCommentsQuery, Result<CommentListResult>>
    {
        public Task<Result<CommentListResult>> Handle(GetPostCommentsQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(factory.CommentListResult is { } list
                ? Result<CommentListResult>.Success(list)
                : Result<CommentListResult>.Success(new CommentListResult([], 0, false, false)));
        }
    }
}
