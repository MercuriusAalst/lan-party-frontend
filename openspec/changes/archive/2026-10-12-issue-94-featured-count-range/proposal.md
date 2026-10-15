## Why

The inline homepage featured editor was built around an exact-four contract:
four draft positions, a Save button that stays disabled until every position is
filled, and copy that asks for exactly four tournaments. The product requirement
is now one to four featured tournaments, so an administrator who wants a single
lead card, or a lead plus one or two rows, cannot save at all. A saved selection
of fewer than four tournaments is also padded back to four by the read path.

## What Changes

- Accept a featured selection of one to four distinct eligible tournaments. The
  front end enables **Save changes** for any selection in that range, disables it
  with a localized minimum-one hint when nothing is selected, and keeps the add
  controls disabled once four are selected.
- Render the draft as the compact public arrangement: the actual selected cards,
  a lead card first and the remaining cards as rows, with no empty placeholder
  positions. Removing a card compacts the remaining cards and promotes the first
  remaining card to the lead position.
- Make the low-inventory hint informational. One to three eligible tournaments
  are a normal, saveable state; the editor only explains the missing inventory
  when no eligible tournament exists.
- Keep the existing draft semantics: the draft stays local until a confirmed
  save, **Cancel** discards it, and a rejected or failed save keeps the draft and
  the saved baseline intact.
- Align the mock back end with the confirmed contract: a save of one to four
  distinct eligible tournaments is accepted, the saved count and order are
  preserved, a no-longer-eligible saved tournament is backfilled only up to the
  saved selection count, and an unconfigured selection defaults to up to four.
- Reuse the existing endpoints, DTOs, and page wiring. No API, DTO, or route
  change.

## Capabilities

### New Capabilities

### Modified Capabilities

- `featured-tournament-curation`: the persisted and rendered featured selection
  changes from exactly four to one to four tournaments, the editor drafts the
  compact arrangement instead of four positions, and the saved-count fallback
  replaces the pad-to-four fallback.

## Impact

- `src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentEditor.razor(.cs/.css)`
- `src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentCards.razor(.css)`
- `src/Mercurius.LAN.Web/Mock/MockBackendStore.cs`
- `src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json`, `translations.nl-BE.json`
- `tests/Mercurius.LAN.Web.ContractTests/FeaturedTournamentEditorTests.cs`
- `tests/Mercurius.LAN.Web.E2ETests/FeaturedTournamentEditorE2ETests.cs`
