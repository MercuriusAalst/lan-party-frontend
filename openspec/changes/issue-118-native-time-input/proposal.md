# Change: Use a native time input for leaderboard elapsed time

Fastest-time result entry and correction should use a single native HTML time input with millisecond precision, instead of four separate unit fields or free text. The dialog converts the value to the existing positive integer millisecond API value.

## Motivation

Administrators asked for a native time input field rather than separated numeric fields or free text. Browsers render `<input type="time" step="0.001">` as hours, minutes, seconds, and milliseconds segments within one control.

## Scope

Update only the leaderboard dialog input (add form and inline edit), its localized validation text, its focused tests, and the elapsed-duration requirement. Scores, API contracts, DTOs, and displayed duration formatting remain unchanged.

## Assumptions

- The maximum entry is `23:59:59.999`, the limit of a native time input.
- 12-hour or 24-hour display and AM/PM follow the browser locale and cannot be forced.
