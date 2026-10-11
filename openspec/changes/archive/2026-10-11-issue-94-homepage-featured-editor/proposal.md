## Why

The homepage features four tournaments as one large lead card plus three smaller
cards, but administrators can only change that selection from a separate
`/admin/featured-tournaments` page. That page renders a detached preview of the
same arrangement, hides the edit action from the surface it changes, and makes
reordering a cumbersome one-select-per-slot exercise. Issue FE#94 asks for
curation to happen on the homepage itself, next to the cards a visitor sees,
with drag-and-drop as the primary reorder interaction.

## What Changes

- Add an administrator-only **Edit** action to the homepage "Featured
  tournaments" section that opens the curation editor inline, in place of the
  read-only cards, using the same one-lead-plus-three arrangement.
- Make the inline editor reorder by dragging the actual cards, including across
  the lead card and the secondary rows, with a visible drag handle, drop
  feedback, and a compact position control as the keyboard and single-pointer
  alternative. Add and remove tournaments from the eligible inventory in the
  same surface.
- Keep the draft local: the public cards and hero MUST NOT change until a save
  succeeds. **Cancel** discards the draft, a failed save keeps the draft and the
  saved baseline intact, and editor controls stay guarded while loading or
  saving.
- Reuse the existing featured read/write contract, the paginated eligible
  inventory, and the exactly-four-distinct-eligible save guard. No backend,
  DTO, or API change.
- Remove the separate admin page, its organizer-navigation entry, the
  tournaments-browse admin link, the protected-route entry, and the copy that
  only the removed page used.

## Capabilities

### New Capabilities

### Modified Capabilities

- `featured-tournament-curation`: curation moves from a separate preview page
  reached from organizer navigation to an inline homepage editor with
  drag-and-drop reordering, add/remove, and local draft semantics.

## Impact

- `src/Mercurius.LAN.Web/Components/Pages/Home.razor(.cs/.css)`
- `src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentCards.razor(.css)`
- `src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentEditor.razor(.cs/.css)`
- `src/Mercurius.LAN.Web/Components/Pages/Admin/FeaturedTournaments.razor(.cs/.css)` (removed)
- `src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor`
- `src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor`
- `src/Mercurius.LAN.Web/Extensions/LocalReturnUrlHelper.cs`
- `src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json`, `translations.nl-BE.json`
- `tests/Mercurius.LAN.Web.ContractTests/`
- `tests/Mercurius.LAN.Web.E2ETests/`
