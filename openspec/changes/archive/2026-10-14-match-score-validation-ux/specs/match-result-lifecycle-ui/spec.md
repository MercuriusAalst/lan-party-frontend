## ADDED Requirements

### Requirement: Match score entry follows the configured series format

The match details UI MUST explain the required wins for the configured Best-of format and the valid losing-score range. Player score submissions and administrator resolutions MUST accept only integer scores where one participant reaches the required wins and the other has a lower, nonnegative score. The UI MUST block incomplete or invalid scores before invoking the backend, while the backend remains authoritative.

#### Scenario: Player enters a decisive score

- **WHEN** a player enters a score with one participant at the required wins and the other below it
- **THEN** the UI MUST allow submission
- **AND** it MUST submit the participant scores in their displayed order

#### Scenario: Player enters an incomplete or invalid score

- **WHEN** either score is missing, negative, tied, exceeds the required wins, or neither participant reaches the required wins
- **THEN** the UI MUST disable score submission
- **AND** it MUST show accessible guidance explaining the valid score for the configured format
- **AND** the submission handler MUST NOT invoke the backend

#### Scenario: Administrator enters an incomplete or invalid resolution score

- **WHEN** an administrator score resolution has missing or invalid series scores
- **THEN** the UI MUST disable resolution
- **AND** the resolution handler MUST NOT invoke the backend

### Requirement: Score validation errors are actionable and safe

The match details UI MUST display a nonempty backend validation message for HTTP 400 score-action responses when one is available. It MUST preserve the existing unauthorized, forbidden, and conflict mappings. For other failures, including HTTP 500, it MUST use the generic localized fallback and MUST NOT expose the response body.

#### Scenario: Backend rejects a score with a validation error

- **WHEN** a score action receives HTTP 400 with a user-facing validation message
- **THEN** the UI MUST show that message

#### Scenario: Backend failure has no safe validation message

- **WHEN** a score action receives a response without a supported validation message or a non-400 server failure
- **THEN** the UI MUST show its generic localized fallback
- **AND** it MUST NOT expose a server error body
