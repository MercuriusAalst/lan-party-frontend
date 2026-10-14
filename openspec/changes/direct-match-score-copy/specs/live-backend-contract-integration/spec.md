## MODIFIED Requirements

### Requirement: Removed legacy match mutation route is not used

The front end MUST NOT send the removed `PUT /v1/lan/matches/{matchId}` request. The Refit boundary
MUST NOT declare that route or its update request DTO, and the tournament service MUST NOT expose a
match-score update method backed by it. Match score changes MUST use the supported lifecycle commands
(submit score, forfeit, resolve, reverse). The removed confirm-ended command MUST NOT be sent or
declared. Mock mode MUST NOT expose the removed update
path, and mock fixtures MUST continue to work through the supported lifecycle commands. No front-end
page called the removed route, so removing it prevents stale boundary usage rather than fixing a
live-user failure.

#### Scenario: Live match score change uses a lifecycle command

- **WHEN** the front end changes a match result in live mode
- **THEN** it MUST send a supported lifecycle command
- **AND** it MUST NOT send `PUT /v1/lan/matches/{matchId}`

#### Scenario: Removed route is absent from the contract

- **WHEN** the Refit client and tournament service are inspected
- **THEN** the removed match-update route, method, and update DTO MUST NOT be present
- **AND** the resolve command `POST /v1/lan/matches/{matchId}/resolve` MUST remain available

#### Scenario: Mock mode has no removed update path

- **WHEN** a user exercises match lifecycle actions in mock mode
- **THEN** the mock MUST apply the supported lifecycle commands
- **AND** it MUST NOT expose the removed match-update method
