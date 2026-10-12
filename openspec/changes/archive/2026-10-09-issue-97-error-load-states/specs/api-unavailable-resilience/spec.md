## MODIFIED Requirements

### Requirement: Same-route status-page retries rerun initial loading

When a load error is rendered as a status page with a retry action targeting the current route, selecting Retry MUST rerun that page's initial data load without requiring the user to leave the route first, and MUST NOT force a full browser reload when the page can rerun the load through its own component lifecycle. Link-based navigation actions MUST remain available alongside the retry action.

#### Scenario: Profile data becomes available after a failed request

- **WHEN** the Profile page displays its load-error state and its API request becomes available
- **AND** the user selects Retry
- **THEN** the page MUST rerun the initial profile request in place
- **AND** the profile form MUST render when that request succeeds

#### Scenario: Team summary becomes available after a failed request

- **WHEN** Manage Teams displays its load-error state and its API request becomes available
- **AND** the user selects Retry
- **THEN** the page MUST rerun the initial team-summary request in place
- **AND** the team-management state MUST render when that request succeeds

#### Scenario: Public team profile becomes available after a failed request

- **WHEN** a public team profile displays its unavailable state and its API request becomes available
- **AND** the visitor selects Retry
- **THEN** the page MUST rerun the team-profile load in place
- **AND** the team roster MUST render when that request succeeds

#### Scenario: Public participant profile becomes available after a failed request

- **WHEN** a participant profile displays its unavailable state and its API request becomes available
- **AND** the viewer selects Retry
- **THEN** the page MUST rerun the participant-profile load in place
- **AND** the participant profile MUST render when that request succeeds
- **AND** an unknown participant MUST keep showing the unavailable state instead of the unreachable not-found copy

#### Scenario: A page cannot rerun its load in the current circuit

- **WHEN** a status page is rendered outside a page component that owns a reloadable load
- **THEN** its primary action MAY navigate to the same route instead
- **AND** the recovery action MUST still be labelled as a retry
