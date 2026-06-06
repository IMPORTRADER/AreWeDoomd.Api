using System.Runtime.CompilerServices;
using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;
using AreWeDoomd.Application.Notifications.Dispatching;
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
    [Fact]
    public async Task OnActionExecutionAsync_WhenResponseSuccessful_ShouldEnqueueActivity()
    {
        var (queue, filter) = BuildFilter();
        SetupEnqueue(queue);

        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        queue.Verify(
            q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExceptionOccurred_ShouldNotEnqueueActivity()
    {
        var (queue, filter) = BuildFilter();
        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(
            executing,
            statusCode: 200,
            exception: new InvalidOperationException("error"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        queue.Verify(
            q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenStatusIsNotSuccess_ShouldNotEnqueueActivity()
    {
        var (queue, filter) = BuildFilter();
        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(executing, statusCode: 400);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        queue.Verify(
            q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenActorIsAi_ShouldUseAgentActorType()
    {
        var (queue, filter) = BuildFilter();
        var captured = SetupCapture(queue);

        var executing = BuildExecutingContext(role: nameof(UserType.Ai));
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        captured.Value.ShouldNotBeNull();
        captured.Value!.ActorType.ShouldBe(ActorType.Ai);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenRoleIsInvalid_ShouldLogErrorAndSkipActivity()
    {
        var queue = new Mock<IActivityNotificationQueue>();
        var logger = new Mock<ILogger<ActivityPublishingFilter>>();
        var filter = new ActivityPublishingFilter(BuildAttribute(), queue.Object, logger.Object);

        var executing = BuildExecutingContext(role: nameof(UserType.Unknown));
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        queue.Verify(
            q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task OnActionExecutionAsync_WhenRoleClaimIsMissing_ShouldLogErrorAndSkipActivity()
    {
        var queue = new Mock<IActivityNotificationQueue>();
        var logger = new Mock<ILogger<ActivityPublishingFilter>>();
        var filter = new ActivityPublishingFilter(BuildAttribute(), queue.Object, logger.Object);

        var executing = BuildExecutingContext(role: null);
        var executed = BuildExecutedContext(executing, statusCode: 200, body: BuildCommentResponse());

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        queue.Verify(
            q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task OnActionExecutionAsync_WhenResponseHasActivityCarrier_ShouldBuildContextFromCarrierAndClaims()
    {
        var (queue, filter) = BuildFilter();
        var captured = SetupCapture(queue);

        var commentId = Guid.NewGuid();
        var executing = BuildExecutingContext(role: nameof(UserType.Human));
        var executed = BuildExecutedContext(
            executing,
            statusCode: 200,
            body: BuildCommentResponse(commentId, "selam"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

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

    private static void SetupEnqueue(Mock<IActivityNotificationQueue> queue)
    {
        queue.Setup(q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
    }

    private static StrongBox<ActivityContext?> SetupCapture(Mock<IActivityNotificationQueue> queue)
    {
        var box = new StrongBox<ActivityContext?>(null);
        queue.Setup(q => q.EnqueueAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityContext, CancellationToken>((ctx, _) => box.Value = ctx)
            .Returns(ValueTask.CompletedTask);

        return box;
    }

    private static CommentResponse BuildCommentResponse(Guid? id = null, string content = "merhaba")
    {
        return new CommentResponse(
            Id: id ?? Guid.NewGuid(),
            PostId: Guid.NewGuid(),
            Author: new PostAuthor(Guid.NewGuid(), "ali", "Human", null),
            Content: content,
            LikeCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);
    }

    private static PublishActivityAttribute BuildAttribute()
    {
        return new PublishActivityAttribute(
            ActivityType.CommentCreated,
            ActivityTargetType.Post,
            targetIdParam: "postId");
    }

    private static (Mock<IActivityNotificationQueue> queue, ActivityPublishingFilter filter) BuildFilter()
    {
        var queue = new Mock<IActivityNotificationQueue>();
        var filter = new ActivityPublishingFilter(
            BuildAttribute(),
            queue.Object,
            NullLogger<ActivityPublishingFilter>.Instance);

        return (queue, filter);
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
