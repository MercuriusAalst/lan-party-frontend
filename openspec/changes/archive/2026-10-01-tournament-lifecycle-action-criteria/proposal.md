## Why

Tournament detail currently exposes every lifecycle action for every status. Several actions are rejected by the backend because the status, registration count, bracket type, or result state does not permit them. In mock development, the navigation exposes an admin persona without a direct return to the user persona.

## What Changes

- Show or disable tournament lifecycle actions according to the backend's supported status and data prerequisites.
- Preserve backend problem details for lifecycle failures and refresh the tournament after stale-state or validation failures.
- Add a mock-only user persona link for an authenticated mock administrator.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `organizer-workflows`: lifecycle controls reflect the backend's current action criteria and explain missing prerequisites.
- `site-navigation`: mock administrators can return to the user persona from the navigation menu.

## Impact

- Front end only: tournament detail, tournament service/API response handling, navigation, localization, focused contract tests, and OpenSpec artifacts.
- No back-end route or DTO changes. The backend remains authoritative for authorization, state transitions, and validation.
