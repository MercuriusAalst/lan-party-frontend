# Refine team registration popup

## Why
The captain registration popup exposes teams that cannot satisfy the tournament roster size and makes the three-step flow difficult to scan and navigate. Its confirmation state and disabled primary action also need clearer visual feedback.

## What changes
- Show captained teams only when their current member count meets the tournament's required team size, including restored selections and existing registrations.
- Tighten the popup's team, roster, summary, navigation, and confirmation presentation while preserving the existing registration and backend contracts.
- Keep the three steps keyboard accessible and prevent navigation past invalid team or roster state.
- Localize all changed copy in en-US and nl-BE.

## Impact
This changes only the front-end tournament registration experience and its OpenSpec coverage. No API or backend contract changes are required.
