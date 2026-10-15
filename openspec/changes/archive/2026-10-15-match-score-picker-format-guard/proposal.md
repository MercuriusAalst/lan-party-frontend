# Hide score controls for an unsupported match format

## Why

When a match carries a format the front-end does not recognise, the match details dialog still rendered the "Who won?" winner choice for the player and administrator forms. With no required wins for that format, no choice can produce a valid score, so the controls were unusable. In addition, the player and administrator action sections in the dialog were left-aligned in a way that looked unbalanced next to the centred dialog header.

## What Changes

- For an unsupported or unknown match format, render no score-selection control, no result summary and no submit or resolve button. Show a short localized explanation instead.
- Centre the "Your player actions" and administrator action sections of the match dialog, including their titles, score picker groups, summaries, guidance and action buttons.

## Impact

Updates the `match-result-lifecycle-ui` score entry requirement and adds a layout requirement for the dialog action sections. The backend contract and its validation are unchanged and remain authoritative.
