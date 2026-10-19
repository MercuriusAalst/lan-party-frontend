## MODIFIED Requirements

### Requirement: Hybrid administrator result entry

While a leaderboard tournament is in progress, an authenticated administrator MUST be able to record an attempt for an existing leaderboard participant, for an existing LAN user who is not yet in the leaderboard, or for a new free-text guest display name. The first attempt for a new linked user or guest MAY establish that participant. Result entry MUST NOT be available to non-administrators and MUST be disabled once the tournament no longer accepts leaderboard records.

#### Scenario: Selecting a linked LAN user

- **WHEN** an administrator selects a LAN user in the result-entry dialog
- **THEN** the selected user MUST remain identifiable in the selection control after the search results close
- **AND** if that user is already a leaderboard participant, their attempt history MUST be selected
- **AND** if that user has no participant yet, the dialog MUST show the empty-history state

#### Scenario: Switching participant mode

- **WHEN** an administrator switches between existing participants, linked users, and guests
- **THEN** prior participant and user selections, guest text, search results, and history MUST be cleared
- **AND** a guest MUST NOT display participant attempt history

### Requirement: Attempt history, correction, and removal

Administrators MUST be able to inspect a participant's recorded attempts while managing the tournament, correct an accidentally entered attempt, and remove an attempt while the tournament remains in progress. A rejected mutation MUST leave actionable feedback and MUST NOT leave the rejected value on screen as if it had been stored, and a stale attempt-concurrency rejection MUST surface as a recoverable conflict that refreshes the authoritative leaderboard.

#### Scenario: Removing a participant's final attempt

- **WHEN** an administrator removes the final recorded attempt for a participant
- **THEN** the participant MUST be removed from the leaderboard
- **AND** removing one of several attempts MUST retain the participant and their remaining attempts

### Requirement: Leaderboard localization and responsive behavior

All new leaderboard configuration, presentation, result-entry, history, lifecycle, and error copy MUST be localized in the supported en-US and nl-BE resources. The leaderboard and administrator result-entry flows MUST remain usable on desktop and mobile layouts, and long guest display names or a larger number of participants MUST NOT break the layout.

#### Scenario: Result-entry dialog is readable in either theme

- **WHEN** an administrator opens the result-entry dialog in light or dark theme
- **THEN** dialog labels, inputs, selection results, and history MUST remain readable with sufficient text and control contrast
- **AND** the dialog MUST use concise result-entry headings without a redundant outer title or highest-score explanation
