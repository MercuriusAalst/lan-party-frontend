# Delta for tournament-registration-ui

## ADDED Requirements

### Requirement: Started elimination registrations cannot be removed by administrators

For single and double elimination tournaments, administrators MUST NOT remove individual or team registrations after the tournament leaves Scheduled. This restriction MUST be enforced in the admin interface and by the backend. It MUST NOT change leaderboard removal behavior.

#### Scenario: Administrator opens a started elimination tournament

- **WHEN** an administrator views individual or team registrations for a single or double elimination tournament whose status is not Scheduled
- **THEN** the participant list remains visible
- **AND** removal controls are disabled
- **AND** the page explains that registrations are locked after the tournament starts

#### Scenario: Administrator attempts removal after the tournament leaves Scheduled

- **WHEN** an administrator attempts to remove an individual or team registration from a single or double elimination tournament whose status is not Scheduled
- **THEN** the registration MUST remain unchanged

#### Scenario: Administrator removes a scheduled elimination registration

- **WHEN** an administrator removes an individual or team registration from a Scheduled single or double elimination tournament
- **THEN** the existing removal behavior remains available

#### Scenario: Administrator removes a leaderboard registration

- **WHEN** an administrator removes a leaderboard participant after the tournament leaves Scheduled
- **THEN** existing leaderboard removal behavior remains unchanged
