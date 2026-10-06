## OpenSpec-first implementation

- [x] Add `Leaderboard` bracket configuration with a required ranking metric to the create and edit tournament forms, and hide match format, finals format, and round or match scheduling fields that do not apply.
- [x] Add leaderboard DTOs, `ILANClient` methods, service wrappers, and mock backend parity for the public leaderboard, admin leaderboard history, and admin attempt create, correction, and removal.
- [x] Render a leaderboard instead of a generated bracket in the main competition area, with tournament-level metric identification and leaderboard-appropriate section and action wording.
- [x] Present rank, participant display name, and metric-aware best result for live and final rankings, including competition ranks for ties and an intentional no-results-yet state.
- [x] Add the admin hybrid result-entry flow covering existing participant, existing LAN user, and free-text guest selection, plus attempt history, correction, and removal.
- [x] Align leaderboard lifecycle behavior: start with zero participants without expecting matches, live leaderboard state while in progress, final guest-capable tied placements on completion, and a clean scheduled state after reset.
- [x] Suppress tournament registration actions for leaderboard tournaments on tournament detail and tournament list surfaces.
- [x] Localize all new leaderboard copy in en-US and nl-BE and keep loading, empty, error, unauthorized, not-found, and blocked-action states visible.
- [x] Keep the leaderboard and admin result-entry flows usable on desktop and mobile layouts, including long guest display names and larger participant lists.

## Tests and validation

- [x] Add contract coverage for leaderboard DTO shapes, `ILANClient` routes, tournament ranking-metric fields, and mock/live parity.
- [x] Add state coverage for rank and tie display, metric value formatting, hybrid participant selection, first-attempt participant creation, duplicate guest names, attempt correction and removal, and controls disabled after completion or reset. Covered through mock/backend contract tests because the project has no component test harness.
- [x] Confirm existing non-leaderboard tournament, registration, bracket, match, placement, sponsor, and prize behavior remains unchanged.
- [x] Validate the OpenSpec change strictly, then run restore, build, and focused tests; rebuild Tailwind only if the source style entry changes. New leaderboard styling uses component-scoped CSS, so the Tailwind source entry is unchanged.
- [x] Validate the primary leaderboard and admin result-entry flows with Playwright at representative desktop and mobile viewport sizes. Verified by the browser tester at desktop 1440x1000 and mobile 390x844 with all flows passing; artifacts under `output/playwright/issue66`.
