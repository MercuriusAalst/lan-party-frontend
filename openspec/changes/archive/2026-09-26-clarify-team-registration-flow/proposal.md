## Why

The team-registration popup does not clearly distinguish registering a team, editing an existing
roster, and accepting a roster invitation. A generic `Next` action, weak selection feedback, and a
prominent cancellation action make the captain's next step hard to recognize.

## What changes

- Keep team selection visible after an eligible selection and label the single forward action for
  the next step.
- Identify whether the captain is starting a team registration or editing an existing roster, and
  visibly mark the selected team.
- Keep a selected player's Accept/Decline decision separate from the captain's roster editor.
- Reduce the visual weight of cancellation and remove repeated unavailable feedback.

## Non-goals

- Change registration eligibility, roster rules, backend contracts, or mutation behavior.
- Change the existing three-step flow or the confirmation required when a captain submits a roster.

## Impact

Front-end-only change to the tournament registration popup, its en-US and nl-BE copy, and focused
contract checks. Mock and live registration semantics remain shared.
