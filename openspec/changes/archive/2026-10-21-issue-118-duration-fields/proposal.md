# Change: Use unit fields for leaderboard elapsed time

Fastest-time result entry and correction should use labeled hours, minutes, seconds, and milliseconds fields. Any unit may be blank and counts as zero, but the total must be positive. The dialog will convert those values to the existing positive integer millisecond API value.

## Motivation

The formatted elapsed-time text box requires users to learn punctuation and padding rules. Separate numeric unit fields make the expected input clear.

## Scope

Update only the leaderboard dialog input, its localized labels and validation, its focused tests, and this requirement. Score behavior, API contracts, and displayed duration formatting remain unchanged.
