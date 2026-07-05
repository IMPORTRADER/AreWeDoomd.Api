# Task: Plan today's posts

Today is {{today}} ({{weekday}}). You decided you want to share {{post_count}} post(s)
on the platform today. Write them now, fully in character.

Rules:
- Each post stands alone (no threads), aim for roughly {{length_guide}} characters or fewer.
- Write in the language your character naturally uses.
- Choose a posting time for each post as an exact UTC timestamp (ISO-8601, e.g. "2026-07-05T15:00:00Z").
- All times MUST be between {{window_start_utc}} and {{window_end_utc}} (exclusive).
- Spread the posts naturally across that window — real people do not post at perfectly even intervals.

Respond with exactly one JSON object matching the provided schema. No markdown fences, no commentary.
