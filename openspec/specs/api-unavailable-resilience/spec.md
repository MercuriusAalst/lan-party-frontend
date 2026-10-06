# api-unavailable-resilience Specification

## Purpose
TBD - created by archiving change improve-api-unavailable-resilience. Update Purpose after archive.
## Requirements
### Requirement: Live API failures are bounded without automatic retries

The front-end MUST use a bounded timeout for live API requests and MUST NOT automatically retry a failed read request before returning control to the calling UI.

#### Scenario: Live API is unavailable
- **WHEN** a live API request cannot connect or does not complete within the configured timeout
- **THEN** the request fails within the bounded timeout
- **AND** the client MUST NOT issue an automatic retry that adds another wait interval

### Requirement: Independent page sections preserve partial success

The front-end MUST isolate independently useful API-backed page sections so a failure in one section does not discard content successfully loaded by another section. Each section MUST clear its own loading state and render when its request completes, without waiting for an unrelated section.

#### Scenario: One home section fails
- **WHEN** one home-page data request fails and another independent request succeeds
- **THEN** the successful section remains visible
- **AND** the failed section shows a friendly unavailable state or is safely omitted according to its presentation role

#### Scenario: Tournament data fails while sponsors succeed
- **WHEN** the tournament overview cannot load tournaments but sponsor data loads successfully
- **THEN** the tournament area shows a recoverable unavailable state
- **AND** the sponsor section and site navigation remain usable

### Requirement: Lifecycle transport failures do not terminate browsing

Expected API and realtime transport failures MUST be handled within component lifecycle work so they do not terminate the interactive circuit or prevent navigation. Detached lifecycle work MUST be cancelled or guarded by component ownership/version state before it starts realtime work or requests a render after disposal.

#### Scenario: Authenticated page API is unavailable
- **WHEN** an authenticated user opens Profile or Manage Teams while the API is unavailable
- **THEN** the page shows a recoverable unavailable state instead of an unhandled component failure
- **AND** unrelated navigation remains usable

#### Scenario: Optional navigation enrichment is unavailable
- **WHEN** profile, notification, or realtime enrichment for shared navigation fails
- **THEN** the navigation renders with available identity information and remains interactive
- **AND** the failure MUST NOT block initial browsing

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

