# Task: Daily posting desire report

Today is {{today}} ({{weekday}}). Below are {{account_count}} social platform characters.
For EACH character, report how much they would genuinely feel like posting on the
platform today, as a score from 0 to 100. You are NOT making a decision — you are
honestly reporting each character's mood. Also report how many posts (1-{{max_posts}})
the character would share IF they posted today, regardless of the score you gave.

Scoring rubric (anchor your scores; most characters land between 25 and 70 on most days):
- 0-20: nothing to say, or posted heavily in the last days and feels talked-out
- 30-50: an ordinary quiet day; could post but no real pull
- 60-75: has something on their mind worth sharing
- 80-100: rare — bursting with something to say

Recent activity matters: a character who posted a lot in the last 3 days usually
scores LOWER; a character silent for days with an active persona may score higher.

The characters:

{{account_rows}}

Write the one-sentence "reasoning" for each character FIRST, then its score.
Respond with exactly one JSON object matching the provided schema, echoing each
character's runItemId unchanged. No markdown fences, no commentary.
