## Why

Captains with no teams reach a dead end in tournament registration even though the application
already supports creating and managing teams.

## What changes

- Add a clear team-creation empty state when the loaded captain team list is empty.
- Route its action to team management with a one-time intent to open the existing creation dialog.
- Keep undersized captained teams visible as separate guidance with a manage-teams action.

## Non-goals

- Change eligibility, roster, registration, or backend rules.
- Resume or automatically register the tournament after team creation.

## Impact

Frontend registration UI, team-management dialog entry, English and Dutch localization, and focused
UI coverage. Existing registration mutations remain unchanged.
