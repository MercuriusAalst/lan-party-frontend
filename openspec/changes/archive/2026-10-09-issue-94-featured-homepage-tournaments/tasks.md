## 1. OpenSpec and contract

- [x] 1.1 Record the featured-tournament capability delta with RFC 2119 requirements and scenarios for admin curation, exact-four validation, homepage ordering, unsaved preview, and fallback/recovery.
- [x] 1.2 Confirm the frozen cross-repo contract with the backend worker: public `GET /v1/lan/featured-tournaments` returning `{tournamentIds, tournaments}` with minimal card summaries (`id`, `name`, `imageUrl`, `status`, `bracketType`, `format`), and admin `PUT` accepting and returning `{tournamentIds}` with 400 ProblemDetails and existing 401/403 behavior.
- [x] 1.3 Strictly validate this change before writing production code (strict validation passed before production code was written).

## 2. Front-end contract and service layer

- [x] 2.1 Add the `FeaturedTournamentsDTO` and `FeaturedTournamentIdsDTO` request/response shapes under `DTOs/Tournaments/`.
- [x] 2.2 Add the `GET`/`PUT` methods to `ILANClient` with the exact `/v1/lan/featured-tournaments` Refit route.
- [x] 2.3 Expose featured read/save through `ITournamentService` and `TournamentService`.
- [x] 2.4 Keep mock parity: store the saved selection, implement eligible ordering with retained-order reconciliation and deterministic first-four backfill, and reject invalid saves the way the API does.

## 3. Homepage

- [x] 3.1 Extract the existing one-lead/three-secondary card arrangement into a reusable shared component without changing its look, and keep the existing responsive CSS behavior.
- [x] 3.2 Load featured tournaments from the new endpoint in `Home.razor.cs`, preserve loading/error/empty states and the retry path, and remove the first-page listing dependency.
- [x] 3.3 Render the server-ordered selection, including tournaments that are not on the first public page, and keep navigation to the canonical GUID tournament route.

## 4. Admin editor

- [x] 4.1 Add the admin-only `/admin/featured-tournaments` page and expose it from the admin navigation and the tournament browse page.
- [x] 4.2 Render the same lead/three-secondary preview for the current local arrangement.
- [x] 4.3 Support selecting and replacing tournaments from eligible tournaments and prevent duplicates.
- [x] 4.4 Support drag-and-drop reordering plus accessible move up/down controls.
- [x] 4.5 Require exactly four distinct selections on **Save changes**, show a helpful message when fewer than four eligible tournaments exist, and keep failed saves from replacing the saved baseline.

## 5. Localization and states

- [x] 5.1 Add matching en-US and nl-BE copy for the admin editor, validation messages, and homepage states.
- [x] 5.2 Keep loading, empty, error, and unauthorized states visible and keep the editor usable on desktop and mobile. No browser or physical-DOM render was run for this change (not requested; outside the repository verification rules), so desktop/mobile usability is not browser-verified. The emitted-render and state behavior is covered by the contract tests, and the responsive CSS was reviewed statically.

## 6. Verification

- [x] 6.1 Add contract coverage for the featured DTO shapes, `ILANClient` routes, and mock/live parity for save validation, ordering, fallback, and invalidated selections.
- [x] 6.2 Update the homepage E2E expectations to mirror the featured endpoint instead of the first public listing page. The E2E expectations were updated; the E2E suite was NOT executed because no browser run was performed.
- [x] 6.3 Build the Blazor project and run the focused contract tests; rebuild Tailwind only if the Tailwind entry changes. `dotnet build src\Mercurius.LAN.Web\Mercurius.LAN.Web.csproj --no-restore` passed with 0 errors, and `dotnet test tests\Mercurius.LAN.Web.ContractTests\Mercurius.LAN.Web.ContractTests.csproj --no-restore` reported 379 passed, 0 failed, 0 skipped (including the 14 new featured regressions). Tailwind was intentionally not rebuilt because there is no Tailwind entry or utility-generation change.
- [x] 6.4 Strictly validate this change again after implementation and task synchronization.
