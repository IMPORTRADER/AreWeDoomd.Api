using System.Runtime.CompilerServices;
using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using AreWeDoomd.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class ActivityPublishingFilterTests
{
    private static readonly ActivityNotification StubNotification = new(
        ActivityId: "act_stub",
        ActivityType: ActivityType.CommentCreated,
        OccurredAt: DateTimeOffset.UtcNow,
        Actor: new ActivityActor("u1", ActorType.Human, "Ali"),
        Object: new ActivityObject("c1", ActivityObjectType.Comment, "merhaba"),
        Target: new ActivityTarget("p1", ActivityTargetType.Post),
        Recipients: []);

    [Fact]
    public async Task Success_200OK_HumanRole_Dispatches()
    {
        var (engine, notifier, filter) = BuildFilter();
        SetupDispatch(engine, notifier);

        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExceptionOccurred_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200,
            exception: new InvalidOperationException("error"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonSuccessStatus_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 400);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AiRole_ProducesAgentActorType()
    {
        var (engine, notifier, filter) = BuildFilter();
        var captured = SetupCapture(engine, notifier);

        var executing = BuildExecutingContext(role: nameof(UserType.Ai));
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        captured.Value.ShouldNotBeNull();
        captured.Value!.ActorType.ShouldBe(ActorType.Agent);
    }

    [Fact]
    public async Task InvalidRole_LogsErrorAndSkips()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentHubSender>();
        var logger = new Mock<ILogger<ActivityPublishingFilter>>();
        var filter = new ActivityPublishingFilter(BuildAttribute(), engine.Object, notifier.Object, logger.Object);

        var executing = BuildExecutingContext(role: nameof(UserType.Unknown));
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MissingRoleClaim_LogsErrorAndSkips()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentHubSender>();
        var logger = new Mock<ILogger<ActivityPublishingFilter>>();
        var filter = new ActivityPublishingFilter(BuildAttribute(), engine.Object, notifier.Object, logger.Object);

        var executing = BuildExecutingContext(role: null);
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildsContextFromCarrierBodyAndClaims()
    {
        var (engine, notifier, filter) = BuildFilter();
        var captured = SetupCapture(engine, notifier);

        var commentId = Guid.NewGuid();
        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200,
            body: BuildCommentResponse(commentId, "selam"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        captured.Value.ShouldNotBeNull();
        var ctx = captured.Value!;
        ctx.ActivityType.ShouldBe(ActivityType.CommentCreated);
        ctx.ActorId.ShouldBe("user_test");
        ctx.ActorDisplayName.ShouldBe("Test User");
        ctx.ActorType.ShouldBe(ActorType.Human);
        ctx.TargetId.ShouldBe("post_abc");
        ctx.TargetType.ShouldBe(ActivityTargetType.Post);
        ctx.ObjectId.ShouldBe(commentId.ToString());
        ctx.ObjectType.ShouldBe(ActivityObjectType.Comment);
        ctx.ObjectTextPreview.ShouldBe("selam");
    }

    private static void SetupDispatch(Mock<INotificationEngine> engine, Mock<IAgentHubSender> notifier)
    {
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
    }

    private static StrongBox<ActivityContext?> SetupCapture(Mock<INotificationEngine> engine, Mock<IAgentHubSender> notifier)
    {
        var box = new StrongBox<ActivityContext?>(null);
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .Callback<ActivityContext, CancellationToken>((ctx, _) => box.Value = ctx)
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.SendAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        return box;
    }

    private static CommentResponse BuildCommentResponse(Guid? id = null, string content = "merhaba")
        => new(
            Id: id ?? Guid.NewGuid(),
            PostId: Guid.NewGuid(),
            Author: new PostAuthor(Guid.NewGuid(), "ali", "Human", null),
            Content: content,
            LikeCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);

    private static PublishActivityAttribute BuildAttribute()
        => new(ActivityType.CommentCreated, ActivityTargetType.Post, targetIdParam: "postId");

    private static (Mock<INotificationEngine> engine, Mock<IAgentHubSender> notifier, ActivityPublishingFilter filter) BuildFilter()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentHubSender>();
        var filter = new ActivityPublishingFilter(
            BuildAttribute(),
            engine.Object,
            notifier.Object,
            NullLogger<ActivityPublishingFilter>.Instance);
        return (engine, notifier, filter);
    }

    private static ActionExecutingContext BuildExecutingContext(string? role)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["postId"] = "post_abc";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user_test"),
            new(ClaimTypes.Name, "Test User")
        };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private static ActionExecutedContext BuildExecutedContext(
        ActionExecutingContext executing,
        int statusCode,
        object? body = null,
        Exception? exception = null)
    {
        return new ActionExecutedContext(
            new ActionContext(executing.HttpContext, executing.RouteData, executing.ActionDescriptor),
            new List<IFilterMetadata>(),
            new object())
        {
            Result = new ObjectResult(body) { StatusCode = statusCode },
            Exception = exception,
            ExceptionHandled = exception is null
        };
    }
}
