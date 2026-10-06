# tournament-registration-ui Specification

## MODIFIED Requirements

### Requirement: Tournament registration stays inside the application

The tournament detail page MUST use the internal registration workflow and MUST NOT present an
external registration URL as the registration path. Leaderboard tournaments MUST NOT expose a
registration surface because leaderboard participation is created only through authorized
administrative result entry.

#### Scenario: Visitor opens a scheduled tournament

- **WHEN** a visitor views a scheduled non-leaderboard tournament detail page
- **THEN** the page MUST expose an in-application registration surface
- **AND** no registration action MAY navigate to Google Forms or another external form

#### Scenario: Visitor opens a closed tournament

- **WHEN** a non-leaderboard tournament is no longer scheduled
- **THEN** the page MUST show registration as closed and MUST NOT offer a registration mutation

#### Scenario: Visitor opens a leaderboard tournament

- **WHEN** a visitor views a leaderboard tournament detail page or tournament list entry
- **THEN** the page MUST NOT offer a registration action or registration mutation
- **AND** any related explanation MUST NOT imply that registration is required, pending, or merely
  closed
