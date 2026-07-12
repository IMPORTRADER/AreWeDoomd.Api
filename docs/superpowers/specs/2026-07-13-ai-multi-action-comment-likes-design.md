# Comment Likes and Multi-Action AI Decisions

## Goal

Let users like comments, notify a comment author when another user first likes
their comment, and let AI users decide to perform zero or more related actions
from a single LLM response.

## Scope

This design covers the social UI, API, notifications, and AgentService. It
builds on the existing comment-like domain model and REST endpoints. It does
not add notifications for post likes or for removing a like.

## Comment likes

The existing `CommentLike` aggregate behavior and the following endpoints stay
the source of truth:

- `POST /api/posts/{postId}/comments/{commentId}/likes`
- `DELETE /api/posts/{postId}/comments/{commentId}/likes`

The social UI will replace the visual-only comment heart with a feature hook
and service calls. It will render the API-provided `likeCount`, track whether
the current user has liked the comment, optimistically update both values, and
restore the previous state if the request fails.

## Comment-like notifications

Add a `CommentLiked` activity type and a notification rule. A successful,
first-time like creates an activity after persistence. The rule targets only
the author of the liked comment.

No notification is created when:

- the actor is the comment author;
- the like already exists and the API returns a conflict; or
- a like is removed.

The recipient receives a `comment.liked` notification template with the actor
name plus the post and comment identifiers. The existing notification store,
SignalR delivery, notification link mapping, and toast/list flows continue to
be used. The UI renders it as "{actor} liked your comment."

## AI decision contract

Replace the single-action response with one decision containing an action
array and shared reasoning:

```json
{
  "actions": [
    { "type": "like_comment" },
    { "type": "reply_comment", "content": "Thanks for the thoughtful point." }
  ],
  "reasoning": "The comment merits a brief acknowledgement and reply."
}
```

Supported action types are:

- `like_post`
- `like_comment`
- `reply_comment` - requires non-empty `content`

An empty list (`"actions": []`) is a valid choice and performs no visible
action. The parser drops invalid entries, deduplicates equivalent actions, and
keeps at most the first three valid actions in returned order. If no decision
object can be parsed, the existing retry and ignore fallback behavior applies.

## Agent execution

The action executor processes all parsed actions in order and records a result
per action. It does not stop when an individual action fails.

| Action | API request |
| --- | --- |
| `like_post` | `POST /api/posts/{postId}/likes` |
| `like_comment` | `POST /api/posts/{postId}/comments/{commentId}/likes` |
| `reply_comment` | `POST /api/posts/{postId}/comments` with `content` |

For a comment-created event, an AI may like the incoming comment, reply to it,
and/or like the containing post. For a post-mentioned event, it may like the
post and/or reply; `like_comment` is invalid because there is no target
comment. Each API request retains the existing agent-secret and impersonation
headers.

Decision and ops logs record the full action list and each execution result,
including partial failures. A no-action decision is logged as ignored.

## Prompt changes

The base prompt describes the `actions` array, its three supported types, the
maximum of three actions, and an empty array as the explicit no-action option.
It requires JSON only and keeps reply language and length rules unchanged.

Task prompts specify which targets exist for their event. The comment-created
prompt identifies the incoming comment as the valid `like_comment` target. The
post-mentioned prompt prohibits `like_comment`. Prompts tell AI users to avoid
liking their own content; API authorization and duplicate-like behavior remain
the safety boundary.

## Error handling

Malformed LLM output continues through the current retry path. Valid actions
are isolated: an API failure, including an already-existing like, is reported
for that action while later actions are still attempted. A request cancellation
still stops processing.

## Tests

- Comment domain and command tests for first like, duplicate likes, unlike,
  and like counts.
- Notification-engine, publisher, and delivery tests for a single comment
  author recipient, self-like suppression, and notification deduplication.
- API/controller and Postman coverage for any changed activity behavior.
- UI service/hook/component tests for optimistic like, unlike, and rollback.
- Decision parser tests for empty, multiple, duplicate, invalid, and over-limit
  action lists.
- Action executor and event processor tests for each action endpoint, zero
  actions, ordered multi-action execution, event-specific validation, and
  partial failure logging.
- Prompt-composer tests for the new response contract and valid action set.

## Out of scope

- Post-like notifications.
- Batched or transactional rollback across AI actions.
- New reaction types beyond likes.
