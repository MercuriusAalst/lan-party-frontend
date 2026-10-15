# Featured homepage tournaments

## Why

The homepage currently derives its four "Featured tournaments" from the first
four items of the public tournament listing (`Home.razor.cs`, `_tournaments.Take(4)`),
so the lead card and the three secondary cards are an accident of the listing
order. Admins cannot choose which tournaments represent the event. The curated
order also has to be identical for every visitor and survive deployments, so it
must be persisted server-side rather than in browser state.

## What Changes

- Add a public featured-tournament read (`GET /v1/lan/featured-tournaments`) that
  returns the ordered selected tournament ids together with the minimal card
  summaries the homepage renders. This lets the homepage show a curated
  tournament that is not on the first page of the public listing without loading
  full tournament graphs (registrations, rosters, matches, placements,
  leaderboards).
- Add an admin-only **Featured tournaments** editor under the existing admin
  area with the same one-lead/three-secondary card arrangement as the homepage,
  selection and replacement from eligible tournaments, duplicate prevention,
  drag-and-drop reordering plus accessible move up/down controls, and a
  **Save changes** action.
- Enforce exactly four distinct eligible selections when saving, and show a
  helpful message when fewer than four eligible tournaments exist.
- Render the saved order on the homepage with the existing large lead plus three
  smaller cards, keep unsaved editor changes local, and keep the default and
  recovery behavior when no selection was ever saved or a saved tournament is no
  longer eligible.
- Add mock backend parity, en-US/nl-BE copy, and focused contract coverage.

## Non-goals

- No general-purpose homepage builder and no change to tournament ordering
  anywhere else.
- No client-side authority over eligibility, validation, or persistence; the
  backend remains the source of truth.
- No new packages and no component test harness; the project has none today.

## Capabilities

### New Capabilities

- `featured-tournament-curation`: Administrator curation of four ordered
  featured tournaments, and the public homepage rendering of that saved order
  with default and recovery behavior.

### Modified Capabilities

## Impact

- `src/Mercurius.LAN.Web/APIClients/ILANClient.cs`
- `src/Mercurius.LAN.Web/DTOs/Tournaments/`
- `src/Mercurius.LAN.Web/Services/ITournamentService.cs`
- `src/Mercurius.LAN.Web/Services/TournamentService.cs`
- `src/Mercurius.LAN.Web/Mock/MockBackendDocument.cs`, `MockBackendStore.cs`, `MockServices.cs`
- `src/Mercurius.LAN.Web/Components/Shared/` (shared featured card presentation)
- `src/Mercurius.LAN.Web/Components/Pages/Home.razor(.cs/.css)`
- `src/Mercurius.LAN.Web/Components/Pages/Admin/FeaturedTournaments.razor(.cs/.css)`
- `src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor`
- `src/Mercurius.LAN.Web/Extensions/LocalReturnUrlHelper.cs`
- `src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json`, `translations.nl-BE.json`
- `tests/Mercurius.LAN.Web.ContractTests/`
- `tests/Mercurius.LAN.Web.E2ETests/PublicSiteTests.cs`
