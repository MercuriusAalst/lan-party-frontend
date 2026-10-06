## ADDED Requirements

### Requirement: Same-route status-page retries rerun initial loading

When a load error is rendered as a status page with a retry action targeting the current route, selecting Retry MUST rerun that page's initial data load without requiring the user to leave the route first.

#### Scenario: Profile data becomes available after a failed request

- **WHEN** the Profile page displays its load-error state and its API request becomes available
- **AND** the user selects Retry
- **THEN** the page MUST rerun the initial profile request
- **AND** the profile form MUST render when that request succeeds

#### Scenario: Team summary becomes available after a failed request

- **WHEN** Manage Teams displays its load-error state and its API request becomes available
- **AND** the user selects Retry
- **THEN** the page MUST rerun the initial team-summary request
- **AND** the team-management state MUST render when that request succeeds
