## Why

Tournament detail pages already surface a partner placement, but the prominent public
tournament titles on the tournaments overview and the home page show no sponsor
attribution at all. The public tournament list projection also drops the
`sponsorPlacement` that the API already returns for each tournament, so list-based
surfaces cannot show which partner presents a tournament even though the back end
supplies the information.

## What Changes

- Show the localized "Presented by {Sponsor}" attribution with the prominent public
  tournament title on the tournaments overview cards, the home page featured cards, and
  the tournament detail hero, reusing the existing
  `Feature.tournaments.partnerHeading` resource in every supported language.
- Omit the attribution when a tournament has no placement or a blank sponsor name, and
  never reword, replace, or persist the tournament name.
- Carry the existing singular `sponsorPlacement` through the public tournament list
  projection so list surfaces have the same partner information as the detail response,
  without adding an endpoint, model class, or dependency.

## Capabilities

### New Capabilities

- `sponsored-tournament-titles`: prominent public tournament titles show the presenting
  partner, and the public tournament list and detail projections carry the existing
  sponsor placement.

### Modified Capabilities

None.

## Impact

- Front end only: tournament list/detail model projection, mock list projection, home
  featured cards, tournaments overview cards, tournament detail hero, scoped component
  styling, and focused contract tests.
- No API route, back-end DTO, package dependency, or database change. The back-end
  response shape is unchanged and stays authoritative.
