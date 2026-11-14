# Tasks

- [x] Update `LanEvent:MenuSections` in `appsettings.json`: drink prices (+0.50 EUR except water and beer), add Bell For (6.2°) at 3 EUR, remove the four discontinued snacks, add bitterballen and chicken nuggets (6 pcs) at 2.50 EUR, set croque and curryworst to 2 EUR.
- [x] Add an optional `IncludedSauces` list to `MenuSectionOptions` and configure Ketchup (tomato / curry), Mayonnaise and Andalouse on Snacks.
- [x] Render the included sauces and the included-sauce note in the menu column on the Info page, reusing the menu-board styling.
- [x] Add English and Dutch locale keys for the new items, sauces and note; remove keys for the discontinued items.
- [x] Update the Info page E2E test for the new prices, the removed items and the sauce note in English and Dutch.
- [x] Build the solution and run the contract tests and the Info page E2E tests.
- [x] Validate the change (by hand; the `openspec` CLI was not available locally).
- [x] Review: drop the fragile removed-items text loop from the E2E test and force a fresh page load per viewport.
- [x] Archive the specification delta into `openspec/specs/lan-info-page`.
