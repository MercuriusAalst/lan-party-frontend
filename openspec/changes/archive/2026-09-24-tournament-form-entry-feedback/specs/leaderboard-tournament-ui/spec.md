## ADDED Requirements

### Requirement: Unavailable result entry explains how to proceed

While a leaderboard tournament is not in progress, the administrator result-entry control MUST stay unavailable, and the interface MUST explain why it is unavailable and how to proceed. The control MUST be presented as clearly disabled rather than as a clickable action, and the explanation MUST be localized and associated with the control for assistive technology.

#### Scenario: Admin views a scheduled leaderboard tournament

- **WHEN** an administrator views a leaderboard tournament that has not started
- **THEN** the result-entry control MUST be disabled
- **AND** the interface MUST show localized copy explaining that results can be recorded once the tournament is in progress

#### Scenario: Admin views a finished leaderboard tournament

- **WHEN** an administrator views a leaderboard tournament that is completed, canceled, or reset
- **THEN** the result-entry control MUST be disabled
- **AND** the interface MUST show localized copy explaining that results can no longer be recorded

#### Scenario: Disabled result entry is accessible

- **WHEN** the result-entry control is disabled
- **THEN** the explanatory copy MUST be associated with the control for assistive technology
- **AND** the disabled control MUST NOT look enabled or clickable

#### Scenario: In-progress result entry is unaffected

- **WHEN** the tournament is in progress
- **THEN** the result-entry control MUST stay enabled
- **AND** the unavailable explanation MUST NOT be shown
