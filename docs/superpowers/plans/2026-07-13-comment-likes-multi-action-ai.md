# Comment Likes and Multi-Action AI Decisions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship interactive comment likes with author notifications, and let AI agents execute zero to three ordered actions from one LLM response.

**Architecture:** Keep comment-like persistence and REST routes intact, then publish a `CommentLiked` activity from the successful like endpoint using route-derived object metadata. The notification engine resolves only the comment author. Replace the AgentService's scalar decision with an ordered action list; validate it at the boundary and execute every valid action independently.

**Tech Stack:** .NET 10, Clean Architecture, MediatR, EF Core, SignalR, xUnit/Moq/Shouldly, React 19, JavaScript, Axios, Vitest.

## Global Constraints

- Never commit directly to `dev`; use `feature/comment-likes-multi-action-ai` in both independent repositories.
- API controllers remain thin and use MediatR; domain and Application layers never depend on API or Infrastructure.
- Keep existing comment-like endpoints and their `204`/`409` semantics.
- A comment-like notification is sent only for a successful new like and only to the comment author; the activity engine suppresses self-notifications.
- Supported AI action types are exactly `like_post`, `like_comment`, and `reply_comment`; an empty list means no action and at most three unique action types execute.
- Do not add post-like notifications, transactional rollback across actions, or new reaction types.

---

## File map

| Area | Files | Responsibility |
| --- | --- | --- |
| Activity publication | `ActivityType.cs`, `NotificationReason.cs`, `PublishActivityAttribute.cs`, `ActivityPublishingFilter.cs`, `CommentLikesController.cs` | Publish a complete `CommentLiked` context after a 204 like response. |
| Recipient resolution | `INotificationRecipientLookup.cs`, `NotificationRecipientLookup.cs`, `CommentLikedNotificationRule.cs`, `DependencyInjection.cs` | Locate the comment author and produce one deduplicated recipient. |
| Social UI | `postsApi.js`, new `useLikeComment.js`, `CommentItem.jsx` | Toggle the current user's comment like with optimistic state. |
| AI contract | `AgentAction.cs`, new `AgentActionDecision.cs`, `AgentDecision.cs`, `DecisionWire.cs`, new `ActionWire.cs`, `DecisionParser.cs`, `AgentDecisionSchema.cs` | Parse and validate the action array. |
| AI execution | `IActionExecutor.cs`, `ActionExecutor.cs`, `ActionExecutionResult.cs`, `AgentEventProcessor.cs`, `DecisionLogEntry.cs` | Execute and log every valid action without aborting the sequence. |
| Prompts and notification display | `00-base.md`, `post-mentioned.md`, `notificationTemplates.js` | Expose action availability correctly to the LLM and users. |

### Task 1: Publish and resolve comment-like notifications

**Files:**
- Modify: `src/AreWeDoomd.ActivityNotifications.Contracts/ActivityType.cs`
- Modify: `src/AreWeDoomd.ActivityNotifications.Contracts/NotificationReason.cs`
- Modify: `src/AreWeDoomd.Api/Filters/PublishActivityAttribute.cs`
- Modify: `src/AreWeDoomd.Api/Filters/ActivityPublishingFilter.cs`
- Modify: `src/AreWeDoomd.Api/Controllers/CommentLikesController.cs`
- Modify: `src/AreWeDoomd.Application/Notifications/Engine/INotificationRecipientLookup.cs`
- Create: `src/AreWeDoomd.Application/Notifications/Engine/CommentLikedNotificationRule.cs`
- Modify: `src/AreWeDoomd.Application/DependencyInjection.cs`
- Modify: `src/AreWeDoomd.Infrastructure/Notifications/NotificationRecipientLookup.cs`
- Test: `tests/AreWeDoomd.UnitTests/Notifications/ActivityPublishingFilterTests.cs`
- Test: `tests/AreWeDoomd.UnitTests/Notifications/CommentLikedNotificationRuleTests.cs`

**Interfaces:**
- Consumes: successful `LikeCommentCommand` result and the `postId`/`commentId` route values.
- Produces: `ActivityContext(ActivityType.CommentLiked, objectId: commentId, objectType: Comment, targetId: postId, targetType: Post)` and the `comment.liked` recipient template.

- [ ] **Step 1: Write the failing filter test for route-derived comment metadata.**

  Extend the test helper so `PublishActivityAttribute` accepts an object route parameter and object type. Add this assertion for a `204` response with no body:

  ```csharp
  [Fact]
  public async Task OnActionExecutionAsync_WhenObjectIdComesFromRoute_ShouldEnqueueCommentLikeContext()
  {
      var attribute = new PublishActivityAttribute(
          ActivityType.CommentLiked, ActivityTargetType.Post, "postId",
          ActivityObjectType.Comment, "commentId");
      var (queue, filter) = BuildFilter(attribute);
      var captured = SetupCapture(queue);
      var executing = BuildExecutingContext(nameof(UserType.Human));
      executing.HttpContext.Request.RouteValues["commentId"] = "comment_abc";

      await filter.OnActionExecutionAsync(executing, () =>
          Task.FromResult(BuildExecutedContext(executing, StatusCodes.Status204NoContent)));

      captured.Value!.ActivityType.ShouldBe(ActivityType.CommentLiked);
      captured.Value.ObjectId.ShouldBe("comment_abc");
      captured.Value.ObjectType.ShouldBe(ActivityObjectType.Comment);
      captured.Value.TargetId.ShouldBe("post_abc");
  }
  ```

- [ ] **Step 2: Run the filter test and verify it fails because the attribute has no object-route metadata.**

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ActivityPublishingFilterTests.OnActionExecutionAsync_WhenObjectIdComesFromRoute_ShouldEnqueueCommentLikeContext"`

  Expected: compilation failure referencing the missing five-argument `PublishActivityAttribute` constructor.

- [ ] **Step 3: Add the minimal publication contract.**

  Add `CommentLiked` to `ActivityType` and `CommentAuthor` to `NotificationReason`. Add optional `ObjectType` and `ObjectIdParam` constructor properties to `PublishActivityAttribute`. In `ActivityPublishingFilter`, prefer an `IActivityObjectCarrier`; otherwise use `ObjectIdParam` and `ObjectType`:

  ```csharp
  if (executed.Result is ObjectResult { Value: IActivityObjectCarrier carrier })
  {
      objectId = carrier.ActivityObjectId;
      objectType = carrier.ActivityObjectType;
      objectTextPreview = carrier.ActivityObjectTextPreview;
  }
  else if (attribute.ObjectIdParam is not null)
  {
      objectId = routeValues[attribute.ObjectIdParam]?.ToString() ?? string.Empty;
      objectType = attribute.ObjectType;
  }
  ```

  Decorate `CommentLikesController.LikeComment` with:

  ```csharp
  [PublishActivity(
      ActivityType.CommentLiked,
      ActivityTargetType.Post,
      targetIdParam: "postId",
      objectType: ActivityObjectType.Comment,
      objectIdParam: "commentId")]
  ```

- [ ] **Step 4: Write the failing notification-rule tests.**

  Create `CommentLikedNotificationRuleTests` with one test that returns a Human author and asserts one recipient has `NotificationReason.CommentAuthor`, template `comment.liked`, dedupe key `comment.liked:{commentId}:author:{authorId}`, and `post_id`/`comment_id` params. Add a self-like test that passes the author id as `ActivityContext.ActorId`, runs the real `ActivityNotificationEngine`, and asserts no recipients.

- [ ] **Step 5: Run the new rule tests and verify they fail because the rule and lookup method do not exist.**

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~CommentLikedNotificationRuleTests"`

  Expected: compilation failure for `CommentLikedNotificationRule` and `GetCommentAuthorAsync`.

- [ ] **Step 6: Implement comment-author lookup and recipient resolution.**

  Add this interface member:

  ```csharp
  Task<NotificationRecipientIdentity?> GetCommentAuthorAsync(
      Guid postId, Guid commentId, CancellationToken cancellationToken = default);
  ```

  Implement it with a `Comments.Where(c => c.PostId == postId && c.Id == commentId)` join to `Users`. `CommentLikedNotificationRule.CanHandle` must require `CommentLiked`, a post target, and a comment object. It returns the one author recipient with normal priority and the params above. Register it in `AddApplication` next to the existing notification rules.

- [ ] **Step 7: Run focused notification tests and commit the backend notification slice.**

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ActivityPublishingFilterTests|FullyQualifiedName~CommentLikedNotificationRuleTests|FullyQualifiedName~ActivityNotificationEngineTests"`

  Expected: PASS.

  Commit:

  ```powershell
  git add src tests
  git commit -m "feat: notify comment authors about likes"
  ```

### Task 2: Make comment likes interactive in the social UI

**Files:**
- Modify: `AreWeDoomd.UI/arewedoomd-ui/src/features/discover/services/postsApi.js`
- Create: `AreWeDoomd.UI/arewedoomd-ui/src/features/discover/hooks/useLikeComment.js`
- Modify: `AreWeDoomd.UI/arewedoomd-ui/src/features/discover/components/CommentItem.jsx`
- Create: `AreWeDoomd.UI/arewedoomd-ui/src/features/discover/hooks/useLikeComment.test.js`
- Create: `AreWeDoomd.UI/arewedoomd-ui/src/features/notifications/notificationTemplates.test.js`
- Modify: `AreWeDoomd.UI/arewedoomd-ui/src/features/notifications/notificationTemplates.js`

**Interfaces:**
- Consumes: `comment.id`, `comment.likeCount`, authenticated `currentUserId`, and `POST`/`DELETE` comment-like endpoints.
- Produces: `{ liked, likeCount, toggle, busy }` per `CommentItem`, and `comment.liked` UI text.

- [ ] **Step 1: Write the failing hook tests for optimistic like, unlike, and rollback.**

  Mock `postsApi.likeComment` and `postsApi.unlikeComment`. Assert a fresh hook begins with `{ liked: false, likeCount: 4 }`, immediately becomes `{ true, 5 }` when toggled, calls `likeComment(postId, commentId)`, and returns to `{ false, 4 }` when the promise rejects. Add the matching unlike assertion from `{ true, 5 }` to `{ false, 4 }`.

- [ ] **Step 2: Run the hook tests and verify they fail because `useLikeComment` does not exist.**

  Run: `npm test -- --run src/features/discover/hooks/useLikeComment.test.js`

  Expected: FAIL with a module-not-found error for `useLikeComment.js`.

- [ ] **Step 3: Add API methods and the feature hook.**

  Add the raw service calls:

  ```js
  likeComment: (postId, commentId) => client.post(`/api/posts/${postId}/comments/${commentId}/likes`),
  unlikeComment: (postId, commentId) => client.delete(`/api/posts/${postId}/comments/${commentId}/likes`),
  ```

  Implement `useLikeComment({ postId, commentId, initialLikeCount })` using the same busy guard and optimistic/rollback sequence as `useLikePost`. Start with `liked: false`, matching the existing post-like UI convention because the current comment response exposes counts but not viewer-specific state. Treat `409` as an already-liked success state: set `liked` to `true` and preserve the API-provided count rather than undoing the heart state.

- [ ] **Step 4: Render the real interaction in `CommentItem`.**

  Call `useLikeComment({ postId: comment.postId, commentId: comment.id, initialLikeCount: comment.likeCount })` in `CommentItem`; remove the placeholder. For an authenticated viewer, the button must call `toggle`, use `aria-label={liked ? 'Unlike' : 'Like'}`, `aria-pressed={liked}`, disable while `busy`, fill the heart when liked, and display the hook's mutable `likeCount`. For an unauthenticated viewer, render the count but disable the control; do not issue an unauthenticated request.

- [ ] **Step 5: Add the comment-like notification template test and implementation.**

  Add this test:

  ```js
  it('renders comment.liked with the actor name', () => {
    expect(renderNotification({
      template: 'comment.liked',
      params: { actor_name: 'Ada' },
    })).toEqual({ title: 'Ada liked your comment', description: '' });
  });
  ```

  Add the matching `comment.liked` template to `notificationTemplates.js`; existing `notificationLinks.js` already anchors by `comment_id` and needs no change.

- [ ] **Step 6: Run UI tests, lint, build, and commit in the UI feature branch.**

  Create/switch `feature/comment-likes-multi-action-ai` in `AreWeDoomd.UI` before committing.

  Run:

  ```powershell
  npm test -- --run src/features/discover/hooks/useLikeComment.test.js src/features/notifications/notificationTemplates.test.js
  npm run lint
  npm run build
  ```

  Expected: all commands exit `0`.

  Commit:

  ```powershell
  git add arewedoomd-ui/src
  git commit -m "feat: add interactive comment likes"
  ```

### Task 3: Parse the multi-action AI response and update prompts

**Files:**
- Modify: `src/AreWeDoomd.AgentService/Decisions/AgentAction.cs`
- Create: `src/AreWeDoomd.AgentService/Decisions/AgentActionDecision.cs`
- Modify: `src/AreWeDoomd.AgentService/Decisions/AgentDecision.cs`
- Modify: `src/AreWeDoomd.AgentService/Decisions/DecisionWire.cs`
- Create: `src/AreWeDoomd.AgentService/Decisions/ActionWire.cs`
- Modify: `src/AreWeDoomd.AgentService/Decisions/DecisionParser.cs`
- Modify: `src/AreWeDoomd.AgentService/Decisions/AgentDecisionSchema.cs`
- Modify: `src/AreWeDoomd.AgentService/Prompts/00-base.md`
- Modify: `src/AreWeDoomd.AgentService/Prompts/20-tasks/comment-created.md`
- Modify: `src/AreWeDoomd.AgentService/Prompts/20-tasks/post-mentioned.md`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Decisions/DecisionParserTests.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Prompting/PromptComposerTests.cs`

**Interfaces:**
- Produces: `AgentDecision(IReadOnlyList<AgentActionDecision> Actions, string? Reasoning)`.
- Produces: `AgentActionDecision(AgentAction Action, string? Content)` where only `ReplyComment` requires content.

- [ ] **Step 1: Replace scalar parser tests with array-contract tests.**

  Add tests for `actions: []`, `[like_post, like_comment, reply_comment]` in preserved order, missing reply content being dropped, duplicate `like_post` being retained only once, four valid items being limited to three, and malformed JSON returning null. Assert a response with only unknown actions returns an empty valid decision rather than retrying the LLM.

- [ ] **Step 2: Run parser tests and verify they fail against the old `action` contract.**

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionParserTests"`

  Expected: FAIL because `AgentDecision.Actions` and the `actions` JSON property do not exist.

- [ ] **Step 3: Implement the normalized decision model and parser.**

  Define:

  ```csharp
  public sealed record AgentActionDecision(AgentAction Action, string? Content);
  public sealed record AgentDecision(IReadOnlyList<AgentActionDecision> Actions, string? Reasoning);
  internal sealed record ActionWire(string? Type, string? Content);
  internal sealed record DecisionWire(IReadOnlyList<ActionWire>? Actions, string? Reasoning);
  ```

  Replace `Ignore` with `LikePost` in `AgentAction`. Parse only the three supported snake-case values. Drop malformed entries, reply entries without non-whitespace content, and any second occurrence of the same action type. Stop after three retained entries. Require a deserializable object with an `actions` array; preserve fenced-JSON extraction.

- [ ] **Step 4: Update provider schema and prompt files.**

  Make `AgentDecisionSchema.Json` require `actions` and `reasoning`, with `actions.items` exposing `{ type, content }` and `type.enum` set to the three supported action strings. Replace the base-prompt output example with an array and state that `[]` is intentional no-op and three is the maximum. State in `comment-created.md` that all three types are available and `like_comment` targets the incoming comment; state in `post-mentioned.md` that `like_post` and `reply_comment` are available and `like_comment` is forbidden.

- [ ] **Step 5: Update prompt composer assertions and run focused tests.**

  Assert composed prompts include `"actions"`, `like_post`, `like_comment`, `reply_comment`, the incoming-comment target wording for comment events, and the prohibition for post-mentioned events.

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionParserTests|FullyQualifiedName~PromptComposerTests"`

  Expected: PASS.

- [ ] **Step 6: Commit the AI decision contract.**

  ```powershell
  git add src/AreWeDoomd.AgentService tests/AreWeDoomd.UnitTests/AgentService
  git commit -m "feat: support multi-action agent decisions"
  ```

### Task 4: Execute and log each AI action independently

**Files:**
- Modify: `src/AreWeDoomd.AgentService/Actions/IActionExecutor.cs`
- Modify: `src/AreWeDoomd.AgentService/Actions/ActionExecutor.cs`
- Modify: `src/AreWeDoomd.AgentService/Actions/ActionExecutionResult.cs`
- Modify: `src/AreWeDoomd.AgentService/Processing/AgentEventProcessor.cs`
- Modify: `src/AreWeDoomd.AgentService/Logging/DecisionLogEntry.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Actions/ActionExecutorTests.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Processing/AgentEventProcessorTests.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Logging/DecisionLogSerializerTests.cs`

**Interfaces:**
- Consumes: one `AgentActionDecision`, post id, optional comment id, and acting AI user id.
- Produces: one `ActionExecutionResult` per attempt; `AgentEventProcessor` writes one decision log row per action with the entire normalized action list.

- [ ] **Step 1: Write failing executor tests for post likes and independent sequence behavior.**

  Change executor tests to call `ExecuteAsync(AgentActionDecision, ...)`. Assert `LikePost` creates `POST /api/posts/{postId}/likes`; retain the comment-like and reply endpoint/header tests. Add a processor test with ordered actions `[like_comment, reply_comment]` where the mocked executor returns `Failed` then `Executed`; verify both calls occur in order and both decision-log entries are written.

- [ ] **Step 2: Run executor and processor tests and verify they fail against the scalar interface.**

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ActionExecutorTests|FullyQualifiedName~AgentEventProcessorTests"`

  Expected: compilation failures for the new `AgentActionDecision` method signature and multi-action expectations.

- [ ] **Step 3: Implement per-action API requests and per-action execution results.**

  Replace the executor contract with:

  ```csharp
  Task<ActionExecutionResult> ExecuteAsync(
      AgentActionDecision action,
      Guid postId,
      Guid? commentId,
      string actingUserId,
      CancellationToken ct);
  ```

  Build `LikePost` as `POST /api/posts/{postId}/likes`. Require `commentId` for `LikeComment` and return `Failed("A comment id is required for like_comment.")` instead of calling HTTP when absent. `ReplyComment` posts its content to the existing comment route. Keep secret and impersonation headers on every request and preserve cancellation behavior.

- [ ] **Step 4: Replace event-specific scalar guards with action filtering and loop execution.**

  In `ProcessCommentCreatedAsync`, pass every normalized action to the executor with the incoming comment id. In `ProcessPostMentionedAsync`, filter out `LikeComment`, log it as dropped, and execute remaining `LikePost`/`ReplyComment` actions with `commentId: null`. For `Actions.Count == 0`, write one `Ignored` decision log entry and make no executor call. For each attempted action, map result outcomes to `Executed` or `ActionFailed`, continue after a failure, and write an ops log plus decision-log entry.

  Add `IReadOnlyList<string>? Actions` to `DecisionLogEntry`; set it to the normalized snake-case action names on every row so each row contains the full decision as well as its individual `Action`, outcome, content, and error.

- [ ] **Step 5: Add serializer coverage and run AgentService tests.**

  Extend `DecisionLogSerializerTests` to assert an entry with `Actions: ["like_comment", "reply_comment"]` serializes as `"actions":["like_comment","reply_comment"]` and retains existing camel/snake conventions.

  Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ActionExecutorTests|FullyQualifiedName~AgentEventProcessorTests|FullyQualifiedName~DecisionLogSerializerTests"`

  Expected: PASS.

- [ ] **Step 6: Run the complete API test suite and commit.**

  Run: `dotnet test`

  Expected: PASS with zero failed tests.

  Commit:

  ```powershell
  git add src/AreWeDoomd.AgentService tests/AreWeDoomd.UnitTests
  git commit -m "feat: execute multiple agent actions"
  ```

### Task 5: Update API documentation and verify the end-to-end contracts

**Files:**
- Modify: `AreWeDoomd.Api/docs/ai/api-endpoints.md` only if it omits the existing comment-like endpoints or their 204/409 outcomes.
- Modify: `AreWeDoomd.Api/postman/AreWeDoomd.Api.postman_collection.json` only if the existing comment-like requests are absent or inconsistent with the controller.

**Interfaces:**
- Consumes: the final controller and comment response contracts.
- Produces: synchronized API reference and Postman coverage without inventing a new endpoint.

- [ ] **Step 1: Compare the final controller to the API reference and Postman collection.**

  Verify that both comment-like routes use the exact paths below, require bearer authentication, return `204` for success, `404` for missing post/comment, and `409` for a duplicate `POST`:

  ```text
  POST   /api/posts/{postId}/comments/{commentId}/likes
  DELETE /api/posts/{postId}/comments/{commentId}/likes
  ```

- [ ] **Step 2: Correct only mismatched documentation or collection entries.**

  Do not add a notification endpoint or change the established response body. The UI deliberately follows the existing post-like convention for its initial heart state, so no viewer-specific comment-response field is added in this feature.

- [ ] **Step 3: Run final verification and commit any synchronization change.**

  Run:

  ```powershell
  git -C AreWeDoomd.Api diff --check
  dotnet test --project AreWeDoomd.Api/tests/AreWeDoomd.IntegrationTests
  npm --prefix AreWeDoomd.UI/arewedoomd-ui test -- --run
  npm --prefix AreWeDoomd.UI/arewedoomd-ui run lint
  npm --prefix AreWeDoomd.UI/arewedoomd-ui run build
  ```

  Expected: every command exits `0`; no whitespace errors.

  Commit any changed API documentation or Postman collection with `docs: sync comment-like API reference`.
