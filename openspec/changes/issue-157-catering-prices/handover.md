# Handover: issue #157, update the food and drinks price list

- **Worktree:** `.claude/worktrees/issue-157`
- **Branch:** `feat/issue-157-catering-prices` (from `origin/main` @ `1d41cb7`), not committed yet
- **Running app (mock mode, seeded):** http://localhost:5157/info#catering (Docker container `lanfe-157`)

## What changed

### Price list (single source: `appsettings.json` → `LanEvent:MenuSections`)

| Item | Before | After | Why |
| --- | --- | --- | --- |
| Water (still/sparkling) | 1 EUR | 1 EUR | water exempt |
| Fuze Tea | 1 EUR | 1.50 EUR | +0.50 |
| Cola (zero) | 1 EUR | 1.50 EUR | +0.50 |
| Monster Energy | 1.50 EUR | 2 EUR | +0.50 |
| Beer | 1.50 EUR | 1.50 EUR | beer exempt |
| **Bell For (6.2°)** | – | 3 EUR | new |
| Croque Monsieur | 3 EUR | 2 EUR | repriced |
| Curryworst → **Frikandel / curryworst** | 1.50 EUR | 2 EUR | renamed + repriced |
| **Bitterballen (6 pcs / 6 stuks)** | – | 2.50 EUR | new |
| **Chicken nuggets / Kipnuggets (6 pcs / 6 stuks)** | – | 2.50 EUR | new |
| Fryer Snacks Party Mix (8pcs) | 2 EUR | removed | |
| Boulet | 2 EUR | removed | |
| Boulet Special | 3 EUR | removed | |
| Curryworst special | 2 EUR | removed | treated as the issue's "Frikandel special" |

### Sauces
- New optional `IncludedSauces` list on a menu section (`LanEventOptions.MenuSectionOptions`).
- Snacks lists Ketchup (tomato / curry), Mayonnaise and Andalouse.
- The Snacks column ends with "Sauce included with every snack. Choose from:" / "Saus inbegrepen bij elke snack. Kies uit:" followed by the sauces as chips. No sauce price is shown anywhere.

### Files
- `src/Mercurius.LAN.Web/appsettings.json`: new menu data.
- `src/Mercurius.LAN.Web/Options/LanEventOptions.cs`: `IncludedSauces`.
- `src/Mercurius.LAN.Web/Components/Pages/Info.razor` / `.razor.cs` / `.razor.css`: sauce block, name → locale-key mappings (old ones removed), styling that reuses the menu-board tokens (works in light and dark).
- `wwwroot/locales/translations.en-US.json`, `translations.nl-BE.json`: new keys added, dead keys for removed items deleted.
- `tests/Mercurius.LAN.Web.E2ETests/PublicSiteTests.cs`: new E2E test `CateringMenuShowsCurrentPricesAndIncludedSaucesAcrossLocalesAndViewports`.
- `openspec/changes/issue-157-catering-prices/`: proposal, `lan-info-page` delta (catering requirement now covers included sauces), tasks, screenshots, this handover.

## Verification
- `dotnet build Mercurius.LAN.sln -c Release`: succeeded, 0 errors (only existing CS0067 warnings).
- Contract tests: **669/669 passed** (includes the EN/NL locale-key parity test).
- E2E `PublicSiteTests` against the real backend (`lan-party-backend` main @ `ea1cf25`) + Postgres 17, mirroring `ci-e2e.yml`: **43/43 passed**, including the new catering test.
- Screenshots (`screenshots/`): EN/NL × desktop/mobile × light/dark, no horizontal overflow at 1440 px or 390 px.
- No .NET SDK or Node on this machine, so everything ran in Docker (`mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/playwright:v1.55.0-noble`).
- The `openspec` CLI is not installed: the change was validated by hand. Run `openspec validate issue-157-catering-prices --strict` before archiving.

## Seeded data
The menu has no database data; its seed is the configuration above. The app runs in mock mode with the existing local seed `src/Mercurius.LAN.Web/MockData.Local/backend.json` (gitignored, copied from the main checkout into the worktree), so tournaments, sponsors and sign-in personas work for the rest of the site.

## Test flows

| # | Flow | Steps | Expected |
| --- | --- | --- | --- |
| 1 | EN desktop menu | Open http://localhost:5157/info#catering at desktop width | Drinks: Water 1, Fuze Tea 1.50, Cola (zero) 1.50, Monster Energy 2, Beer 1.50, Bell For (6.2°) 3. Snacks: Croque Monsieur 2, Frikandel / curryworst 2, Bitterballen (6 pcs) 2.50, Chicken nuggets (6 pcs) 2.50 (all EUR after the number) |
| 2 | Removed items | Search the page (Ctrl+F) for Boulet, Party Mix, special | No matches in the menu |
| 3 | Sauces | Look under the Snacks column | Check icon + "Sauce included with every snack. Choose from:" and chips Ketchup (tomato / curry), Mayonnaise, Andalouse; no price on sauces |
| 4 | Dutch | Footer language selector → Nederlands, reopen `/info#catering` | Bier, Bell For (6,2°), Bitterballen (6 stuks), Kipnuggets (6 stuks), "Saus inbegrepen bij elke snack. Kies uit:", Ketchup (tomaat / curry), Mayonaise; same prices |
| 5 | Mobile | DevTools device toolbar at 390 px, both languages | Columns stack, prices right-aligned, sauce chips wrap, no sideways scroll |
| 6 | Dark theme | Theme toggle in the header | Note, chips and prices stay readable |
| 7 | Automated | `dotnet test tests/Mercurius.LAN.Web.E2ETests --filter "FullyQualifiedName~CateringMenu"` | Passes |

## Decisions and assumptions
- "Frikandel special" in the issue = the existing "Curryworst special" item (frikandel and curryworst are the same snack), so it was removed. The existing "Curryworst" became "Frikandel / curryworst".
- "Croque" = the existing "Croque Monsieur" item; its name was kept.
- English names use "pcs" and "Chicken nuggets"; Dutch uses the issue wording ("stuks", "Kipnuggets", "Mayonaise", "Ketchup (tomaat / curry)").

## Clean up
```bash
docker rm -f lanfe-157 lanfe-e2e-pg && docker network rm lanfe-e2e
```
