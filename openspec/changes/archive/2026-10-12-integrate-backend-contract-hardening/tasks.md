## 1. Readable API error extraction

- [x] 1.1 Route `Components/Pages/Users/Profile.razor.cs` failure handling through `ApiExceptionExtensions.GetApiError`, removing its private `GetApiErrorAsync`/raw-content reader while keeping legacy JSON-string and plain-text fallbacks
- [x] 1.2 Route `Components/Pages/Users/CompleteProfile.razor.cs` failure handling through `ApiExceptionExtensions.GetApiError`, removing its private `GetApiErrorAsync`/raw-content reader and preserving the existing localized fallbacks
- [x] 1.3 Replace the raw `ex.Content!` toasts in `Components/Pages/Admin/SponsorManagement.razor.cs` with the shared parser and a localized fallback
- [x] 1.4 Replace `ResolveApiError`'s raw-content return in `Components/Pages/Tournaments/Leaderboard/RecordLeaderboardAttemptDialog.razor.cs` with the shared parser while keeping its forbidden/not-found mapping
- [x] 1.5 Add focused contract tests covering an actual `application/problem+json` `ValidationApiException`/`ApiException` flow (message, detail, first `errors` entry, title, code) and the legacy JSON-string and plain-text fallbacks

## 2. team_changed conflict recovery

- [x] 2.1 Surface the parsed error `code` from `Services/TeamService.cs` so callers can detect the exact `team_changed` `409` conflict
- [x] 2.2 In `Components/Pages/Teams/ManageTeams.razor.cs`, handle only the exact `team_changed` `409` by showing localized feedback and reloading the confirmed team summary plus notifications, without resending the mutation
- [x] 2.3 Contain summary-reload and notification-refresh failures in a durable inline recovery notice (reusing the existing page styling, with no new notification-area UI) that keeps the conflict feedback visible, retains the last confirmed team state, and offers a retry, so the page stays usable
- [x] 2.4 Add the `team_changed` conflict copy to `wwwroot/locales/translations.en-US.json` and `translations.nl-BE.json`
- [x] 2.5 Add focused tests for conflict refresh, no-resend, unchanged authorization, and contained reload/notification failures

## 3. Deadline convergence

- [x] 3.1 Add a page-owned bounded, cancellable authoritative tournament refresh in `Components/Pages/Tournaments/TournamentDetail.razor.cs` that starts only when a refreshed backend match projection confirms the effective deadline expired, and continues until the persisted source match `ResultVersion` at or above the expected effective version proves aggregate/bracket convergence
- [x] 3.2 Compute no local winner, score, or bracket advancement and add no defensive direct downstream-slot comparisons; the match dialog and bracket/schedule wrappers may stay unchanged when the page can own the fix
- [x] 3.3 Retain a strictly newer authoritative same-ID match projection when a later aggregate reload returns a stale/older projection, and guard out-of-order responses with a single generation check
- [x] 3.4 Keep the refresh alive across dialog close and cancel it on navigation or disposal
- [x] 3.5 Show recoverable feedback in a durable inline notice, with a retry using the existing UI style, when the bounded refresh is exhausted or fails
- [x] 3.6 Add focused tests for deadline convergence, stale-response preservation, navigation/disposal cancellation, out-of-order responses, and the retry bound
- [x] 3.7 Note the back-end `ReverseAsync` effective-completed read risk in the implementation brief (final handoff brief under the worktree's ignored `.tmp/`) without editing the back end

## 4. Removed legacy match-update route

- [x] 4.1 Grep every caller of `UpdateMatchAsync`/`UpdateMatchScoresAsync` before deleting them (none is expected)
- [x] 4.2 Remove the `PUT /v1/lan/matches/{matchId}` declaration and `UpdateMatchAsync` from `APIClients/ILANClient.cs`, the `UpdateMatchScoresAsync` method from `Services/ITournamentService.cs` and `Services/TournamentService.cs`, the update request DTO, and the mock update path in `Mock/MockServices.cs`
- [x] 4.3 Update mock fixtures and any remaining callers to use the supported lifecycle commands (confirm-ended, submit score, forfeit, resolve, reverse)
- [x] 4.4 Add focused tests asserting the removed route/method are absent, the resolve command is retained, and the mock fixtures still pass

## 5. Validation

- [x] 5.1 Run `openspec validate integrate-backend-contract-hardening --strict` and fix the artifacts until it passes (passed before implementation and again after task synchronisation: `Change 'integrate-backend-contract-hardening' is valid`)
- [x] 5.2 Build `src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj` and run the focused contract test suites; rebuild Tailwind only if utility classes change (passed: Release `dotnet build src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj -c Release --no-restore -p:UseAppHost=false` 0 errors / 2 warnings with `IncludeMockBackend=false`; Debug contract suite 421 passed / 0 failed / 0 skipped; Release contract suite 364 passed / 0 failed / 0 skipped excluding the 57 mock tests; focused classes 144 passed; Tailwind skipped, no utility classes changed)

## 6. Review and archive

- [x] 6.1 Obtain independent review of the implemented diff against this change (independent review passed: general reviewer PASS, deadline/concurrency reviewer PASS on the frozen sources, and a separate tester overall PASS_WITH_MINOR_FINDINGS with coverage-only, non-blocking notes on the deadline and team lanes)
- [x] 6.2 Re-run `openspec validate integrate-backend-contract-hardening --strict` after implementation and task synchronisation (passed: `Change 'integrate-backend-contract-hardening' is valid`)
- [x] 6.3 Archive `integrate-backend-contract-hardening` on the implementation branch before delivery (archived 2026-10-12 as `openspec/changes/archive/2026-10-12-integrate-backend-contract-hardening`; `openspec archive integrate-backend-contract-hardening --yes` applied one added requirement to each of the four live specs and reported `+ 4, ~ 0, - 0, → 0`)
