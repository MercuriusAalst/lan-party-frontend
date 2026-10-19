## MODIFIED Requirements

### Requirement: Hybrid administrator result entry

While a leaderboard tournament is in progress, an authenticated administrator MUST be able to record an attempt for an existing leaderboard participant, for an existing LAN user who is not yet in the leaderboard, or for a new free-text guest display name. The first attempt for a new linked user or guest MAY establish that participant. Result entry MUST NOT be available to non-administrators and MUST be disabled once the tournament no longer accepts leaderboard records.

#### Scenario: First attempt establishes a guest participant

- **WHEN** an administrator records an attempt using a new free-text guest display name
- **THEN** the attempt MUST create that guest participant
- **AND** the leaderboard MUST show the guest with its best result

#### Scenario: First attempt establishes a linked participant

- **WHEN** an administrator records an attempt for an existing LAN user who is not yet in the leaderboard
- **THEN** the attempt MUST establish that participant as a linked user
- **AND** selecting that user again MUST record further attempts against the same participant

#### Scenario: Equal guest display names stay distinct

- **WHEN** an administrator enters the same display name for two separate new guests
- **THEN** the front end MUST create two distinct participants
- **AND** matching display-name text MUST NOT merge them

#### Scenario: Multiple attempts remain possible

- **WHEN** a participant already exists in the leaderboard
- **THEN** the administrator MUST be able to select that participant and record additional attempts
- **AND** the current best attempt MUST be distinguishable from the other attempts

#### Scenario: Invalid metric value is rejected before submission

- **WHEN** an administrator enters a negative or over-precise score, or a non-positive or non-integral duration
- **THEN** the form MUST show inline validation feedback for that ranking metric
- **AND** the invalid value MUST NOT be submitted as a recorded attempt

#### Scenario: Result entry is administrator-only

- **WHEN** a non-administrator views the leaderboard
- **THEN** result-entry, correction, and removal controls MUST NOT be available

#### Scenario: Result entry closes with the tournament

- **WHEN** the tournament is completed, canceled, or reset
- **THEN** result-entry, correction, and removal controls MUST be disabled or hidden

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

#### Scenario: Administrator reviews attempt history

- **WHEN** an administrator inspects a participant with recorded attempts
- **THEN** every recorded attempt for that participant MUST be listed with its metric value

#### Scenario: Administrator corrects an attempt

- **WHEN** an administrator corrects an attempt value while the tournament is in progress
- **THEN** the displayed best result and ranking MUST be recalculated from backend data

#### Scenario: Administrator removes an attempt

- **WHEN** an administrator removes an attempt while the tournament is in progress
- **THEN** the attempt MUST disappear from history
- **AND** the displayed best result and ranking MUST be recalculated from backend data

#### Scenario: Stale attempt is rejected

- **WHEN** the backend rejects a correction or removal because the attempt changed
- **THEN** the front end MUST show a recoverable conflict explanation
- **AND** it MUST refresh the authoritative leaderboard instead of presenting the rejected change as applied

#### Scenario: Attempt mutation fails

- **WHEN** the backend rejects an attempt submission, correction, or removal
- **THEN** the front end MUST show actionable feedback
- **AND** it MUST NOT optimistically leave an incorrect result on screen

#### Scenario: Removing a participant's final attempt

- **WHEN** an administrator removes the final recorded attempt for a participant
- **THEN** the participant MUST be removed from the leaderboard
- **AND** removing one of several attempts MUST retain the participant and their remaining attempts

### Requirement: Leaderboard localization and responsive behavior

All new leaderboard configuration, presentation, result-entry, history, lifecycle, and error copy MUST be localized in the supported en-US and nl-BE resources. The leaderboard and administrator result-entry flows MUST remain usable on desktop and mobile layouts, and long guest display names or a larger number of participants MUST NOT break the layout.

#### Scenario: Leaderboard copy is localized

- **WHEN** the leaderboard surfaces render in either supported language
- **THEN** every new label, state, action, and error message MUST come from the localized resources
- **AND** no raw translation key or placeholder MAY be visible

#### Scenario: Administrator records results on a mobile viewport

- **WHEN** an administrator uses the result-entry flow at a mobile viewport
- **THEN** participant selection, value entry, and confirmation MUST remain reachable and readable

#### Scenario: Long names and many participants remain readable

- **WHEN** participant display names are long or many participants are ranked
- **THEN** the leaderboard MUST remain readable without overlapping or clipped content
- **AND** rank, display name, and best result MUST each remain identifiable

#### Scenario: Result-entry dialog is readable in either theme

- **WHEN** an administrator opens the result-entry dialog in light or dark theme
- **THEN** dialog labels, inputs, selection results, and history MUST remain readable with sufficient text and control contrast
- **AND** the dialog MUST use concise result-entry headings without a redundant outer title or highest-score explanation
