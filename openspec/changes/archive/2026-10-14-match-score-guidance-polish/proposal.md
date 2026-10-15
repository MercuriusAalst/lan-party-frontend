# Polish match score guidance and input parsing

## Why

Review of the score validation UX found three defects. The guidance sentence renders awkwardly for Best of 1 ("may have 0-0 wins"). Typing a non-integer such as `2.5` into a score field fails Blazor's integer binding, so the previous value silently stays in use for validation and submission while the field shows the new text. A match with an unknown format shows guidance that describes an impossible score.

## What Changes

- Replace the single formatted guidance string with natural per-format copy in en-US and nl-BE.
- Parse score field text strictly as a non-negative integer; non-integer, negative, signed or overflowing text counts as an entered but invalid score and blocks submission.
- Omit the guidance element, and its `aria-describedby` reference, when the match format is unknown.

## Impact

Modifies the "Match score entry follows the configured series format" requirement of match-result-lifecycle-ui. No backend contract changes.
