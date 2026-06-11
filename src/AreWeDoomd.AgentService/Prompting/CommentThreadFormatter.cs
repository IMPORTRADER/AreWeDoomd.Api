using AreWeDoomd.AgentService.Context;

namespace AreWeDoomd.AgentService.Prompting;

public static class CommentThreadFormatter
{
    public static string Format(IReadOnlyList<ThreadComment> comments)
    {
        if (comments.Count == 0)
        {
            return "(no comments yet)";
        }

        return string.Join(
            "\n",
            comments.Select(c => $"{c.AuthorUsername} ({c.AuthorUserType}): {c.Content}"));
    }
}
