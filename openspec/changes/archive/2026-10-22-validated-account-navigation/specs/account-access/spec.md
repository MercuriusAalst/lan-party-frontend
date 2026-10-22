## ADDED Requirements

### Requirement: Account navigation exposes only a validated LAN profile

The authenticated account navigation MUST display LAN profile identity data only after a current, completed profile response has been validated for the active authenticated identity. Before validation, the navigation MUST use a neutral account state that does not expose any provider subject, nickname, username, name, or email claim.

#### Scenario: Profile request is pending

- **WHEN** an authenticated user's LAN profile request is still pending
- **THEN** the account navigation MUST show the neutral account state
- **AND** it MUST NOT show any identity value from the authentication claims

#### Scenario: Completed profile is available

- **WHEN** the current profile request returns a completed profile with a LAN username
- **THEN** the account navigation MUST show that LAN username
- **AND** it MUST show the profile picture supplied by that profile response

#### Scenario: Profile is incomplete

- **WHEN** the current profile request returns an incomplete profile
- **THEN** the account navigation MUST remain neutral
- **AND** the existing onboarding flow MUST remain available

#### Scenario: Profile is missing or deleted

- **WHEN** the current profile request returns not found or gone
- **THEN** the account navigation MUST remain neutral
- **AND** deleted-profile handling MUST retain the existing sign-out behavior

#### Scenario: Profile request fails

- **WHEN** the current profile request fails temporarily
- **THEN** the account navigation MUST remain neutral
- **AND** the failure MUST NOT expose authentication claim values or force a sign-out

#### Scenario: Authenticated identity changes

- **WHEN** the authenticated identity changes while a profile request is pending or complete
- **THEN** the navigation MUST immediately discard the previous identity's username and picture
- **AND** a response for the previous identity MUST NOT update the navigation

#### Scenario: Profile notification is stale or invalid

- **WHEN** a current-user profile notification completes for a superseded identity or request
- **THEN** the navigation MUST ignore that response
- **WHEN** a current profile notification fails or returns an incomplete profile
- **THEN** the navigation MUST clear previously displayed profile data
