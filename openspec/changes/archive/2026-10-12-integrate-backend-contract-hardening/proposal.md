## Why

Back-end PRs #146-#149 changed the live contract the front end consumes: failures now return
problem-details bodies, the match read endpoints (`GET /v1/lan/matches/{id}` and
`GET /v1/lan/matches/{id}/me`) now compute effective deadline state in memory while the tournament
aggregate read stays persisted, team mutations can fail with the exact `team_changed` `409` conflict
code, and the legacy `PUT /v1/lan/matches/{matchId}` route is gone. The front end still reads raw
error content, cannot recover from the `team_changed` conflict, only refreshes the single match
dialog after a deadline instead of converging on the authoritative bracket, and still declares the
removed match-update route at its Refit boundary.

## What Changes

- Extract readable API failure text through the existing `ApiExceptionExtensions` parser in Profile,
  CompleteProfile, SponsorManagement, and RecordLeaderboardAttemptDialog, replacing per-page
  raw-content readers while keeping the legacy JSON-string and plain-text fallbacks.
- Recover from the exact backend `team_changed` `409` conflict in team management with localized
  feedback and a reload of the confirmed team summary plus notifications, containing recoverable
  reload/refresh failures and never automatically resending the mutation.
- Add a page-owned, bounded, cancellable authoritative tournament refresh that triggers when a
  refreshed backend match projection confirms an expired deadline and continues until the
  authoritative tournament aggregate and bracket converge, computing no local winner or bracket
  advancement and retaining strictly newer authoritative match projections across stale aggregate
  reloads.
- **BREAKING** Remove the obsolete `PUT /v1/lan/matches/{matchId}` route, `UpdateMatchAsync`,
  `UpdateMatchScoresAsync`, the update request DTO, and the mock update path. No front-end page called
  this route, so removing it prevents stale boundary usage rather than fixing a live-user failure.

## Capabilities

### New Capabilities

None. Every requirement change lands in an existing capability.

### Modified Capabilities

- `error-status-surfaces`: profile, profile-completion, sponsor, and leaderboard failures render a
  readable message extracted by the shared API error parser instead of raw response content.
- `user-owned-team-management`: the exact `team_changed` `409` conflict is recovered with localized
  feedback and a confirmed-state reload that never resends the mutation.
- `match-result-lifecycle-ui`: a refreshed backend match projection confirming the expired deadline
  triggers a page-owned bounded authoritative tournament refresh that converges the aggregate and
  bracket and preserves existing loading/auth/private-projection behavior.
- `live-backend-contract-integration`: the removed legacy match-update route is no longer declared at
  the live boundary.

## Impact

- `src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor.cs`,
  `Components/Pages/Users/CompleteProfile.razor.cs`,
  `Components/Pages/Admin/SponsorManagement.razor.cs`, and
  `Components/Pages/Tournaments/Leaderboard/RecordLeaderboardAttemptDialog.razor.cs`: use the shared
  API error parser.
- `src/Mercurius.LAN.Web/Services/TeamService.cs`, `Components/Pages/Teams/ManageTeams.razor.cs`,
  and the team locale files: `team_changed` conflict recovery.
- `src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentDetail.razor.cs`: page-owned
  deadline-convergence refresh. The match dialog and bracket/schedule wrappers may be left unchanged
  when the page can own the fix; the requirement fixes behavior, not which component implements it.
- `src/Mercurius.LAN.Web/APIClients/ILANClient.cs`, `Services/TournamentService.cs`,
  `Services/ITournamentService.cs`, `DTOs/Matches/*`, and `Mock/MockServices.cs`: removal of the
  legacy match-update route and method.
- `src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json` and `translations.nl-BE.json`: new
  and changed conflict/refresh copy.
- `tests/Mercurius.LAN.Web.ContractTests`: focused contract coverage.
- Verified back-end dependency (read-only): `mercurius-aalst-back-end` PRs #146-#149, heads
  `ce668a63c54fc88c60ec9b54b929265ae3e77680`, `293eb401d825a8ff48cb91f29b12473199c8b36a`,
  `6fe0ad3c8114b10e5004b53bf6668eaf79c141c1`, `6ab0900499b58c4da283f179b68ef0bd35ba75de`.
  No new front-end package or backend edit is expected.
