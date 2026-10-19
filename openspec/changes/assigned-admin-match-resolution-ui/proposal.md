# Require an assigned admin and list matches awaiting resolution

## Why

The backend now requires every tournament to have an assigned administrator and no longer accepts a lone score report automatically: when the score window closes without the opponent's report, or a conflict outlives its correction window, the match waits for the assigned admin. Nothing tells that admin. The front end still lets an admin clear the contact, still promises that a reported score becomes final, and has no place that lists the matches that need a decision.

## What Changes

- Tournament create and edit require an assigned administrator: the picker has no "none" choice, the form shows a validation message and blocks saving, and a legacy tournament without an admin prompts the editor to assign one. The multipart request always carries `AssignedAdminUserId`.
- Add `GET /v1/lan/matches/pending-resolution` (admin only) to the client, service and mock, with `PendingMatchResolutionDTO`.
- Add an admin-only navigation entry with a count badge (refreshed on load and every 30 seconds while the circuit is alive, quiet on failure) and the page `/admin/match-resolutions` that lists the waiting matches and opens the existing match details dialog to resolve them. The page shows loading, empty, error and unauthorized states.
- The match details dialog states why an admin is needed (conflicting reports, or the opponent did not report) and no longer promises automatic acceptance.
- The mock backend stops accepting a lone report automatically and exposes the same pending list for the admin persona.

## Impact

Modifies `match-result-lifecycle-ui`, `organizer-workflows` and `live-backend-contract-integration`. Companion to the backend change `assigned-admin-match-resolution`. In-app only: no email or push notification is added. The backend stays authoritative for who may resolve a match.
