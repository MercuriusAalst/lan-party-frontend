# Update the LAN food and drinks price list

## Why

Issue #157: the catering menu on the Info page no longer matches the menu and prices for the upcoming LAN. Drinks get more expensive (except beer and water), a strong beer is added, several snacks are discontinued or repriced, two snacks are added, and visitors need to know which sauces are available and that a sauce is included with every snack.

## What Changes

- Drinks: Fuze Tea, Cola (zero) and Monster Energy each go up by exactly 0.50 EUR. Water and Beer keep their prices. Bell For (6.2°) is added at 3 EUR.
- Snacks: Boulet, Boulet special, Curryworst special (frikandel special) and the Fryer Snacks Party Mix are removed. Bitterballen (6 pcs) and Chicken nuggets (6 pcs) are added at 2.50 EUR. Croque Monsieur goes to 2 EUR and Curryworst becomes "Frikandel / curryworst" at 2 EUR.
- Menu sections may list available sauces in configuration. The Snacks section lists Ketchup (tomato / curry), Mayonnaise and Andalouse and shows that sauce is included with every snack, without any sauce price.
- All values stay in the existing `LanEvent:MenuSections` configuration; display names are localized in English and Dutch. Layout and styling are kept; the sauce note reuses the existing menu-board styling.

## Impact

- Updates the `lan-info-page` specification (catering requirement gains included-sauce information).
- Code: `appsettings.json`, `LanEventOptions`, `Info.razor(.cs/.css)`, English and Dutch locale files, Info page E2E test.
- No API, backend or database change.
