## 1. Shared status surface

- [x] 1.1 Extend `Components/Shared/StatusPage.razor` with an optional retry callback (event-based) that reruns the owning page's load in place, keeping the existing `PrimaryHref`/`PrimaryForceLoad`/`SecondaryHref` link actions working
- [x] 1.2 Refine `Components/Shared/StatusPage.razor.css` into a balanced, constrained-width composition: contextual eyebrow, single descriptive heading, legible body copy, grouped actions, responsive wrapping, no oversized card and no lateral overflow
- [x] 1.3 Add a restrained status icon/marker only where it is consistent with the existing brand surfaces, keeping it decorative for assistive technology
- [x] 1.4 Keep the alert/status announcement semantics and a single `h1` heading per full-page status surface

## 2. Tournament pages

- [x] 2.1 Replace the `tournament-empty-state` failed-load markup in `Components/Pages/Tournaments/TournamentDetail.razor` with the shared status surface, showing a failure title (never the loading title), a retry callback, and a back-to-tournaments link
- [x] 2.2 Replace the `tournament-empty-state` not-found markup in the same page with the shared not-found variant
- [x] 2.3 Reuse the existing `RetryLoadAsync` entry point in `TournamentDetail.razor.cs` so the retry callback reruns the initial load without a forced reload
- [x] 2.4 Replace the `tournaments-load-error` block in `Components/Pages/Tournaments/TournamentsOverview.razor` with the shared status surface and retain the in-place retry callback
- [x] 2.5 Delete the now-unused `.tournaments-load-error` rules from `TournamentsOverview.razor.css` (the tournament detail stylesheet never owned an error block)

## 3. Remaining full-page failure states

- [x] 3.1 Remove the duplicated status markup in `Components/Pages/Users/CompleteProfile.razor` and use the shared status surface with its existing retry callback and recovery link
- [x] 3.2 Switch `Components/Pages/Users/Profile.razor` and `Components/Pages/Teams/ManageTeams.razor` from `PrimaryForceLoad` to existing lifecycle load methods, preserving their error handling and recovery navigation
- [x] 3.3 Align `Components/Routes.razor`, `Components/Pages/Error.razor`, `Components/Pages/Status/StatusCodePage.razor`, `Components/Pages/Users/PublicUserProfile.razor`, and `Components/Pages/Teams/PublicTeamProfile.razor` with the shared presentation (tone variants only) without changing authorization or server-rendered routes
- [x] 3.4 Give the recoverable public team-profile and participant-profile unavailable states an in-place retry through their existing load lifecycle, keeping a navigation link alongside it and preserving the intentional unknown-profile -> unavailable mapping

## 4. Compact inline errors

- [x] 4.1 Audit the inline errors (tournaments sponsor strip, home sections, tournament search/no-results, public profile match summaries, sponsor admin) and confirm each stays compact and contextual
- [x] 4.2 Add only the minimal markup/style changes needed so no inline failure hides or replaces successfully loaded page content (no inline markup change was required; the existing compact sections already satisfy the requirement)

## 5. Localization

- [x] 5.1 Add or update the status titles, explanations, and action labels in `wwwroot/locales/translations.en-US.json`
- [x] 5.2 Mirror every added or updated key in `wwwroot/locales/translations.nl-BE.json` with natural Dutch copy
- [x] 5.3 Confirm no status surface still renders raw exception text or a missing translation key
- [x] 5.4 Render profile and team-management load failures with localized generic copy instead of raw API response text, and map the team-management load error to a localized key (both languages)

## 6. Verification

- [x] 6.1 Add focused contract checks for the shared status surface contract (single announced heading, retry callback wiring, link-action fallback, localized failure copy) in `StatusPageMarkupContractTests`
- [x] 6.2 Keep the existing E2E error/retry scenarios passing: `TournamentDetailLifecyclePlaywrightTests.TournamentDetailLoadFailureShowsRetryAndRecoversInPlace`, `ProfileAndOnboardingTests.ProfileLoadFailureShowsUnavailableStateAndRecovers`, `TeamManagementPageTests.TeamManagementLoadFailureShowsUnavailableStateAndRecovers`, `PublicSiteTests.TournamentsOverviewLoadFailureShowsTheErrorStateAndRecoversOnRetry`, `PublicSiteTests.HomeTournamentLoadFailureShowsTheRetryStateAndRecoversAfterTheApiIsRestored`, `TeamPublicProfileTests.PublicTeamProfileLoadFailureShowsUnavailableStateAndRecovers`, `TeamPublicProfileTests.UnknownTeamNameShowsNotFoundStatusPage`, `PublicUserProfileTests.PublicUserProfileLoadFailureShowsUnavailableStateAndRecoversInPlace` (passed: final focused E2E run 84 passed / 0 failed / 0 skipped)
- [x] 6.3 Cover representative full-page and inline failures without duplicating near-identical browser cases: reuse the existing tournament-detail not-found, overview failure, home-inline and sponsor-inline rows; adjust the profile and team-management retry rows to the in-place button; and add the shared-surface contract rows from 6.1
- [x] 6.7 Assert the retry pending state at runtime by holding the real tournament load open, then checking the page's loading presentation, no stale failure heading, and no second retry control before releasing it and confirming recovery (passed: `PublicSiteTests.TournamentsOverviewLoadFailureShowsTheErrorStateAndRecoversOnRetry` holds a table lock and asserts the pending overlay; targeted rerun 1 passed / 0 failed)
- [x] 6.4 Run the CI restore/build/test commands for `Mercurius.LAN.Web` and both test projects, and record the real outcome (final independent tester run: `dotnet build Mercurius.LAN.sln -p:UseAppHost=false -v m` 0 errors with 4 NU1900 offline-vulnerability warnings; contract suite 370 passed / 0 failed / 0 skipped; focused E2E across the six touched classes 84 passed / 0 failed / 0 skipped in 2 m 2 s)
- [x] 6.5 Update `tests/Mercurius.LAN.Web.E2ETests/COVERAGE.md` and `VERIFICATION.md` for the added/changed checks
- [x] 6.6 Re-run `openspec validate issue-97-error-load-states --strict` after implementation and task synchronisation
