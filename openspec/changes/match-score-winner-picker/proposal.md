# Pick the match winner and the loser's games won

## Why

Players and administrators currently type two numbers into the match details dialog, so an invalid series score (a tie, an out-of-range value, a non-integer) can be entered and must then be explained and blocked. Error and success toasts also render underneath the open dialog, and the dialog's action buttons stretch across the full width of the panel.

## What Changes

- Replace the two free-text score fields with a winner selection followed by the loser's games won, limited to the values the configured Best-of format allows. The winner's score is set to the required wins automatically.
- Show a live result summary and keep submission or resolution disabled until the selection is complete.
- Show feedback toasts above open dialogs.
- Size the dialog's action buttons to their content.

## Impact

Updates the `match-result-lifecycle-ui` score entry requirement for both the player submit form and the administrator resolve form. The backend contract and its validation are unchanged and remain authoritative.
