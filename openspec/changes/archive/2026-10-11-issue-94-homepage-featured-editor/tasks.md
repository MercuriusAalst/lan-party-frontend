## 1. OpenSpec

- [x] 1.1 Record the modified `featured-tournament-curation` delta for the inline homepage editor, drag-and-drop first interaction, local draft semantics, and add/remove.
- [x] 1.2 Strictly validate this change before writing production code. `openspec validate issue-94-homepage-featured-editor --type change --strict --no-interactive` passed before any source edit.

## 2. Shared editor

- [x] 2.1 Move the `Admin/FeaturedTournaments` editor logic into a shared `FeaturedTournamentEditor` component (featured read, paginated eligible inventory, four positions, save guards) and add add/remove plus Save/Cancel for an inline draft.
- [x] 2.2 Extend `FeaturedTournamentCards` with an editing mode that renders the same lead-plus-three presentation with native HTML5 drag handles, drop feedback, removal, and a compact position control.

## 3. Homepage wiring

- [x] 3.1 Add the administrator-only **Edit** action to the homepage featured section and render the inline editor there.
- [x] 3.2 Apply the confirmed server order to the homepage cards and hero only after a successful save, and discard the draft on **Cancel**.

## 4. Removal of the separate page

- [x] 4.1 Remove the `Admin/FeaturedTournaments` page, its organizer-navigation entry, the tournaments-browse admin link, and the protected-route entry.
- [x] 4.2 Drop the localization copy that only the removed page used and add the inline editor copy in en-US and nl-BE.

## 5. Verification

- [x] 5.1 Retarget the existing editor contract tests to the shared editor, keep the authorization/save-guard and keyed-position enforcement, and cover the inline draft, add/remove, cancel, and confirmed-order behavior. `FeaturedTournamentEditorTests` now holds 15 focused cases.
- [x] 5.2 Build the Blazor project and run the full contract suite. `dotnet build src\Mercurius.LAN.Web\Mercurius.LAN.Web.csproj -p:UseAppHost=false` reported 0 errors, and `dotnet test tests\Mercurius.LAN.Web.ContractTests\Mercurius.LAN.Web.ContractTests.csproj --no-restore` reported 390 passed, 0 failed. Tailwind was not rebuilt because no Tailwind entry or utility class changed; independent validation owns the release-configuration and browser checks.
- [x] 5.3 Strictly validate this change again after implementation and task synchronization.
- [x] 5.4 Give the homepage featured read its own generation so a response that lands after a confirmed save cannot restore the previous order, and cover it with a delayed-read contract test. `ComponentLifecycleBehaviorTests.HomeKeepsTheConfirmedFeaturedOrderWhenTheInitialReadCompletesAfterASave` fails with the guard removed and passes with it; the full contract suite reported 391 passed, 0 failed.
- [x] 5.5 Collapse the editing arrangement to a single column at tablet widths so the position controls keep the row usable from 641-900px. The independent tester owns the browser containment sweep across 1440/1024/900/820/768/700/641/390.
- [x] 5.6 Final independent verification after the last Cancel-markup tweak. An independent reviewer re-reviewed the frozen source and reported PASS with no unresolved findings. An independent tester force-rebuilt the Release app (exit 0; the served web dll hash `57D73291598419A4` contains the always-visible Cancel markup), ran the new editor E2E class 4/4 passed (TRX `issue94-e2e-resume/trx/featured-freeze.trx`, 23s), the affected existing E2E 5/5, the Release focused contract set 56/56, and the full Debug contract suite 391/391. The parent verified the desktop and mobile clips in light, dark, and nl-BE.
- [x] 5.7 Known validation limits. The editor loading and load-error `Cancel` states are covered only by static markup assertions, and the full Release build and the full E2E suite were not run locally; the repository's remote CI covers the full build and suite.
