using System.Security.Claims;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Filters;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Notifications.Engine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class ActivityEmissionFilterTests
{
    private static readonly ActivityNotification StubNotification = new(
        ActivityId: "act_stub",
        ActivityType: ActivityType.CommentCreated,
        OccurredAt: DateTimeOffset.UtcNow,
        Actor: new ActivityActor("u1", ActorType.Human, "Ali"),
        Object: new ActivityObject("", ActivityObjectType.Comment, null),
        Target: new ActivityTarget("p1", ActivityTargetType.Post),
        Recipients: []);

    [Fact]
    public async Task OnActionExecutionAsync_NoAttribute_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: false);
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithAttribute_200OK_Dispatches()
    {
        var (engine, notifier, filter) = BuildFilter();
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        notifier.Verify(
            n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ExceptionOccurred_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 200,
            exception: new InvalidOperationException("error"));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NonSuccessStatus_DoesNotDispatch()
    {
        var (engine, notifier, filter) = BuildFilter();
        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 400);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(50);

        engine.Verify(
            e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_BuildsActivityContextFromRouteAndClaims()
    {
        var (engine, notifier, filter) = BuildFilter();
        ActivityContext? captured = null;
        engine.Setup(e => e.ComputeAsync(It.IsAny<ActivityContext>(), It.IsAny<CancellationToken>()))
              .Callback<ActivityContext, CancellationToken>((ctx, _) => captured = ctx)
              .ReturnsAsync(StubNotification);
        notifier.Setup(n => n.NotifyAsync(It.IsAny<ActivityNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var executing = BuildExecutingContext(withAttribute: true);
        var executed = BuildExecutedContext(executing, statusCode: 200);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        await Task.Delay(100);

        captured.ShouldNotBeNull();
        captured!.ActivityType.ShouldBe(ActivityType.CommentCreated);
        captured.ActorId.ShouldBe("user_test");
        captured.ActorDisplayName.ShouldBe("Test User");
        captured.TargetId.ShouldBe("post_abc");
        captured.ObjectId.ShouldBe("");
        captured.ActorType.ShouldBe(ActorType.Human);
        captured.TargetType.ShouldBe(ActivityTargetType.Post);
        captured.ObjectType.ShouldBe(ActivityObjectType.Comment);
    }

    private static (Mock<INotificationEngine> engine, Mock<IAgentNotifier> notifier, ActivityEmissionFilter filter) BuildFilter()
    {
        var engine = new Mock<INotificationEngine>();
        var notifier = new Mock<IAgentNotifier>();
        return (engine, notifier, new ActivityEmissionFilter(engine.Object, notifier.Object));
    }

    private static ActionExecutingContext BuildExecutingContext(bool withAttribute)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["postId"] = "post_abc";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user_test"),
            new Claim(ClaimTypes.Name, "Test User")
        ], "test"));

        var actionDescriptor = new ControllerActionDescriptor
        {
            EndpointMetadata = withAttribute
                ? (IList<object>)
                [
                    new PublishActivityAttribute(
                        ActivityType.CommentCreated,
                        ActorType.Human,
                        ActivityObjectType.Comment, null,
                        ActivityTargetType.Post, "postId")
                ]
                : new List<object>()
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private static ActionExecutedContext BuildExecutedContext(
        ActionExecutingContext executing,
        int statusCode,
        Exception? exception = null)
    {
        var context = new ActionExecutedContext(
            new ActionContext(
                executing.HttpContext,
                executing.RouteData,
                executing.ActionDescriptor),
            new List<IFilterMetadata>(),
            new object())
        {
            Result = new ObjectResult(null) { StatusCode = statusCode },
            Exception = exception,
            ExceptionHandled = exception is null
        };
        return context;
    }
}
