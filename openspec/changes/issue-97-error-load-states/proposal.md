## Why

The front end's full-page failure states are implemented independently per page. A failed tournament-detail load renders an oversized "loading" heading, a tiny error line, and an orphaned retry button on a near-empty card; other pages hand-roll their own error markup, and `CompleteProfile` duplicates `StatusPage` because the shared component only supported link-based actions. There is no single, intentional visual language for failed loads, unavailable services, forbidden routes, not-found routes, or unexpected server errors.

## What Changes

- Define one shared full-page status presentation (eyebrow, human-readable title, short explanation, grouped actions) on the existing `StatusPage` component and its styles, covering unavailable/transient failure, not-found, forbidden, and unexpected-server-failure variants.
- Add support for retry callbacks on `StatusPage` so same-route recovery reruns the page's initial load in place, while preserving the existing link-based navigation actions.
- Redesign the tournament-detail not-found and failed-load views to use the shared presentation instead of the `tournament-empty-state` markup. The failed state MUST NOT present a loading heading.
- Bring the tournament overview load failure and profile completion failure onto the shared presentation and remove the duplicate `status-page` markup in `CompleteProfile`.
- Keep widget/section-level failures (sponsors, home sections, search, match summaries, sponsor admin) compact, contextual, and non-destructive to already loaded page content.
- Keep loading, empty, no-results, not-found, authorization, and transient-failure states visually and semantically distinct.
- Add localised copy for the new/changed states in `en-US` and `nl-BE`.
- Preserve existing route-level authorization and server-rendered recovery pages.

## Capabilities

### New Capabilities

- `error-status-surfaces`: A single shared presentation for full-page failure, unavailable, forbidden, not-found, and server-failure states, with in-place retry, compact inline variants, and accessible, responsive, localised copy.

### Modified Capabilities

- `api-unavailable-resilience`: same-route status-page retries rerun the page's initial load through the component lifecycle instead of forcing a full browser reload.

## Impact

- `src/Mercurius.LAN.Web/Components/Shared/StatusPage.razor` and `.razor.css`: shared full-page status presentation and retry callback support
- `src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentDetail.razor` / `.razor.cs` / `.razor.css`: not-found and failed-load views
- `src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor` / `.razor.css`: load failure presentation and obsolete error CSS
- `src/Mercurius.LAN.Web/Components/Pages/Users/CompleteProfile.razor` / `.razor.cs`: remove duplicated status markup, use the shared component
- `src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor` / `.razor.cs` and `Components/Pages/Teams/ManageTeams.razor` / `.razor.cs`: in-place retry instead of forced reload
- `src/Mercurius.LAN.Web/Components/Routes.razor`, `Components/Pages/Error.razor`, `Components/Pages/Status/StatusCodePage.razor`, `Components/Pages/Users/PublicUserProfile.razor`, `Components/Pages/Teams/PublicTeamProfile.razor`: alignment with the shared presentation
- `src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json` and `translations.nl-BE.json`: new/changed status copy
- `tests/Mercurius.LAN.Web.E2ETests` and `tests/Mercurius.LAN.Web.ContractTests`: focused error/retry coverage and E2E coverage documentation
