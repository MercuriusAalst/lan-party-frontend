# Show tournament prizes in the detail hero

## Why

The configured prizes only appear in the Eindresultaten section, below the bracket, as plain grey rows. Visitors deciding whether to register never see them without scrolling to the bottom of the page.

## What Changes

- The tournament detail hero shows a prize strip under the badges with a gold, silver or bronze medal, the place label and the prize for every configured top-three prize. The strip stays visible before and after the tournament.
- The placements area no longer repeats the prize list before placements are known; it shows its normal empty state. Prizes under the podium after placements are known are unchanged.
- The strip wraps to one column at narrow widths and uses the existing theme tokens, so it works in light and dark themes.

## Impact

Updates the `game-detail-page-experience` specification. No API, DTO, mock backend or locale change; it reuses the existing prize fields and the `Feature.tournaments.prizes` and `Feature.tournaments.placementPlace` texts. Admin prize editing is out of scope.
