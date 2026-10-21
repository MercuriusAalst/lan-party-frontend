# FE #118 F1 elapsed duration entry

## Why
Fastest-time leaderboard administrators currently enter raw milliseconds, while leaderboard results display as elapsed durations. This makes entry and correction harder to read and invites unit mistakes.

## What Changes
Accept human-readable `m:ss.fff` and `h:mm:ss.fff` values in the existing result dialog, while keeping the API contract as exact positive integer `durationMilliseconds`. Format existing attempts as elapsed durations when editing and localize the labels and guidance in en-US and nl-BE.

## Scenarios
- `1:23.456`, `0:58.032`, and `12:05.789` convert exactly to 83456, 58032, and 725789 milliseconds.
- `1:02:03.004` converts exactly to 3723004 milliseconds; minute fields in hour form and second fields must be 00-59.
- Blank, zero, signed, raw-millisecond, malformed, non-ASCII, over-precise, out-of-range, and overflowing values are rejected without an API call.
- Editing an existing duration prefills its formatted elapsed value and saves unchanged values as the same integer milliseconds.
- Scores and their validation remain unchanged.
