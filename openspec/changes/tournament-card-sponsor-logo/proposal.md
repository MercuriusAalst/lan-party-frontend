# Show the sponsor logo on tournaments overview cards

## Why

Sponsored tournament cards on the tournaments overview only show the text "Presented by <sponsor>". A sponsor is recognised by its logo, and the placement already carries a default logo and an optional light logo for dark backgrounds.

## What Changes

- A sponsored overview card shows a badge in the top-left corner of the tournament image with the localized "Presented by" label and the sponsor logo.
- The badge uses a translucent surface that follows the active theme; in dark mode it shows the light logo through the existing `ThemeAwareSponsorLogo` component.
- When the sponsor has no light logo, the badge stays light in dark mode so the default logo keeps its contrast.
- When the placement has a sponsor name but no logo, the card keeps the existing text attribution.
- When the logo fails to load, the badge shows the sponsor name instead.

## Impact

- Modifies `sponsored-tournament-titles` (overview scenario).
- No backend, API or DTO change: `SponsorLogoUrl` and `SponsorLightLogoUrl` are already part of the public sponsor placement.
- Out of scope: the home featured cards and the tournament detail hero keep the text attribution; the badge is not a link.
