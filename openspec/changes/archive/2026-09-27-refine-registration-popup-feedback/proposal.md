# Refine registration popup feedback

## Why

Captain registration revalidation currently tears down the visible roster state and disables
the focused action while the check runs, so a blocked step looks like the popup reloaded.
Surviving team, roster, and summary cards also stretch to the full dialog width, the clickable
stepper header has a square hover, the summary captain badge sits before the member name, and
one cancellation action is not styled as danger.

## What changes

- Keep the current roster and eligibility presentation on screen during a background roster
  revalidation, and report a failed revalidation with one concise inline reason instead of a
  visible step repaint.
- Let team, roster, and summary cards keep their natural width when only a few options exist.
- Give the clickable stepper header a soft rounded hover surface.
- Show the captain badge after the member name on the summary step.
- Render every registration cancellation action with the danger treatment.

## Impact

Front-end presentation and popup interaction behavior only. Registration, roster confirmation,
and cancellation semantics, the backend contract, and the existing localization keys are
unchanged. No new dependency is required.