## Why

Admins hit two confusing dead ends in the create tournament form today. The participation control renders `Solo` as the visible first option while the bound request value stays unset, so submitting an untouched form fails validation until the admin toggles `Team` and back. When the create request does fail, the dialog renders the raw API response body, including `ProblemDetails` JSON, instead of a readable message. A related dead end exists on leaderboard tournaments, where the result-entry control sits silently disabled whenever the tournament is not in progress, so it looks broken rather than intentionally unavailable.

## What Changes

- Initialize the create tournament form's bound participation value to individual (solo) before the first render and the first validation pass, so the value the form presents as selected is the value that is validated and submitted.
- Replace raw create failure bodies with safe localized feedback: keep specific, safe client-error validation messages, map server errors and internal `ProblemDetails` payloads to a localized generic message, and never render the raw response body.
- Present the create failure as a visible, accessible alert near the top of the form instead of plain helper text.
- Keep the leaderboard result-entry control disabled outside the in-progress state, and add localized explanatory copy plus an accessible disabled affordance so the control no longer looks clickable or inert.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `frontend-tournament-schedule-contract`: the admin create form must initialize its bound participation value before validation, and create failures must surface safe localized feedback instead of raw response bodies.
- `leaderboard-tournament-ui`: the administrator result-entry affordance must explain why it is unavailable and how to proceed when the tournament is not in progress.

## Impact

- Front end only: `CreateTournamentDTO`, `AddTournamentDialog` (markup, code-behind, component styles), `TournamentLeaderboardTab` (markup, code-behind, component styles), the en-US and nl-BE resources, and the `Mercurius.LAN.Web.ContractTests` suite.
- No back-end, API route, DTO contract, or mock-mode contract change. Lifecycle status stays authoritative on the back end, and the front end continues to disable result entry outside the in-progress state.
