## ADDED Requirements

### Requirement: Tournament lifecycle actions reflect backend eligibility

The tournament detail UI MUST show or disable administrator lifecycle actions according to the tournament's current status and available prerequisite data. Backend validation MUST remain authoritative.

#### Scenario: Admin starts a scheduled tournament

- **WHEN** an administrator views a scheduled tournament
- **THEN** the start action MUST be unavailable for unsupported Swiss brackets
- **AND** a non-leaderboard start action MUST be disabled until at least two active registrations match the tournament participation mode
- **AND** a leaderboard start action MUST NOT require registrations

#### Scenario: Admin cancels a tournament

- **WHEN** an administrator views a tournament that is not completed or already canceled
- **THEN** the cancel action MUST be available
- **AND** the cancel action MUST be hidden when the tournament is completed or already canceled

#### Scenario: Admin finishes a tournament

- **WHEN** an administrator views an in-progress tournament
- **THEN** the finish action MUST be available for non-leaderboard brackets
- **AND** the finish action for a leaderboard tournament MUST be disabled until the public leaderboard contains a recorded result
- **AND** the finish action MUST be hidden when the tournament is not in progress

#### Scenario: Admin resets a tournament

- **WHEN** an administrator views a completed or canceled tournament
- **THEN** the reset action MUST be available
- **AND** the reset action MUST be hidden for scheduled or in-progress tournaments

#### Scenario: Admin deletes an in-progress tournament

- **WHEN** an administrator views an in-progress tournament
- **THEN** the delete action MUST be disabled because the backend rejects deletion in that state

#### Scenario: Backend rejects a lifecycle request

- **WHEN** a lifecycle request fails with backend problem details
- **THEN** the UI MUST show the available backend explanation, with a localized fallback when no explanation is present
- **AND** after a validation or concurrency failure the UI MUST refresh tournament state before presenting the actions again
