using System.Text.RegularExpressions;

namespace AreWeDoomd.Application.Notifications.Engine;

/// <summary>
/// Extracts @username mention tokens from raw post/comment content. Usernames
/// are [A-Za-z0-9_]{3,24}; a token does not count when the @ is glued to a
/// word (e.g. an email address) or when the name continues past 24 chars.
/// </summary>
public static class MentionParser
{
    private static readonly Regex MentionRegex = new(
        @"(?<![A-Za-z0-9_@])@([A-Za-z0-9_]{3,24})(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    public static IReadOnlyList<string> Extract(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return MentionRegex
            .Matches(content)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
