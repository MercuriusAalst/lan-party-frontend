# Use neutral accents for badges, secondary buttons and the packing list

## Why

Light green currently appears on informational elements: the shared info badges on tournament cards and detail views, the text of secondary buttons, and the hardware group of the Info packing list. Green should signal primary actions and success or status meaning, so the overuse dilutes it and makes ordinary metadata look like status.

## What Changes

- Informational `.badge` pills render with a neutral surface, border and ink text in both themes. Status pills (`.tournament-status` and its status modifiers) keep their semantic colours.
- Secondary buttons use ink text and a neutral border; the brand green appears only as a hover and focus border. Primary buttons are unchanged.
- The Info packing list hardware group uses a neutral slate accent and tint instead of green; the basic (orange) and optional (blue) groups are unchanged, so the three priority groups remain distinct in light and dark themes.
- Tournaments overview filter chips use a neutral selected-state fill and keep the green border.

## Impact

Updates the `shared-brand-surfaces` and `lan-info-page` specifications. No new palette, package, API or backend change. Remaining light-green usages elsewhere are out of scope and tracked for a follow-up.
