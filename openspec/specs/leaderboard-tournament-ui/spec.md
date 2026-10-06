# leaderboard-tournament-ui Specification

## Purpose
TBD - created by archiving change issue-66-leaderboard-tournament-ui. Update Purpose after archive.
## Requirements
### Requirement: Leaderboard tournament configuration

The front end MUST offer `Leaderboard` as a bracket type when an admin creates or edits a tournament, MUST require a ranking metric of `HighestScore` or `FastestTime`, and MUST treat leaderboard tournaments as individual competitions. Match format, finals format, and round or match scheduling fields MUST NOT be presented as meaningful configuration and MUST NOT block submission.

#### Scenario: Admin creates a score leaderboard

- **WHEN** an admin selects the leaderboard bracket and the highest-score metric while creating a tournament
- **THEN** the form MUST require the ranking metric before submission
- **AND** the submitted configuration MUST be an individual tournament with highest-score ranking

#### Scenario: Admin creates a time leaderboard

- **WHEN** an admin selects the leaderboard bracket and the fastest-time metric while creating a tournament
- **THEN** the form MUST require the ranking metric before submission
- **AND** the submitted configuration MUST be an individual tournament with fastest-time ranking

#### Scenario: Admin edits a scheduled leaderboard

- **WHEN** an admin opens the configuration editor for a scheduled leaderboard tournament
- **THEN** the stored ranking metric MUST be clearly visible
- **AND** the admin MUST be able to change it while the tournament remains scheduled

#### Scenario: Ranking metric is locked after start

- **WHEN** a leaderboard tournament is no longer scheduled
- **THEN** the ranking metric MUST remain visible and MUST NOT be editable

#### Scenario: Match-only configuration is hidden for leaderboards

- **WHEN** the create or edit form targets a leaderboard tournament
- **THEN** match format, finals format, and round or match scheduling controls MUST be hidden or clearly marked as inapplicable
- **AND** submission MUST NOT fail because that match-only configuration is empty

### Requirement: Leaderboard competition surface

The tournament detail page MUST identify a leaderboard tournament and its ranking metric, and the main competition area MUST render a leaderboard instead of a generated bracket. Match-only surfaces such as upcoming match schedules, bracket and round filters, match cards, and match detail actions MUST NOT be presented for leaderboard tournaments, and bracket-oriented section or action wording MUST be adapted to leaderboard wording.

#### Scenario: Anonymous visitor views a leaderboard

- **WHEN** an anonymous visitor opens a leaderboard tournament
- **THEN** the page MUST render the public leaderboard with its ranking metric
- **AND** it MUST NOT render administrator-only result-management controls

#### Scenario: Bracket terminology is replaced

- **WHEN** the tournament is a leaderboard
- **THEN** bracket-oriented labels and anchors such as `View Bracket` MUST use leaderboard wording such as `View Leaderboard`

#### Scenario: Match-only surfaces are suppressed

- **WHEN** the tournament is a leaderboard
- **THEN** upcoming matches, bracket filters, round filters, match cards, and match detail actions MUST NOT be rendered
- **AND** rendering the page MUST NOT require generated match data

### Requirement: Leaderboard ranking presentation

The leaderboard MUST display at least the rank, the participant display name, and the participant's current best result. Score values MUST be presented as scores and duration values MUST be presented as elapsed durations rather than timestamps. Equal best results MUST share the same displayed place using competition ranks such as `1, 1, 3`, and display order MUST follow the backend ordering without introducing a client-side tie-breaker.

#### Scenario: Leaderboard has no results yet

- **WHEN** an in-progress leaderboard tournament has no recorded attempt
- **THEN** the competition area MUST show an intentional no-results-yet state
- **AND** it MUST NOT show an empty bracket, a match schedule, or an error

#### Scenario: Tied best results share a place

- **WHEN** two participants share the same best result and another participant follows
- **THEN** the tied participants MUST display the same place
- **AND** the following participant MUST display the next competition place

#### Scenario: Fastest-time values render as durations

- **WHEN** ranking is by fastest time
- **THEN** each best result MUST be rendered as an elapsed duration
- **AND** it MUST NOT be rendered as a date or clock timestamp

#### Scenario: Best result is the only ranked value

- **WHEN** a participant has several recorded attempts
- **THEN** the leaderboard MUST show only that participant's best value
- **AND** recording a worse attempt MUST NOT change the displayed best result or place

#### Scenario: Live and final ranking use backend data

- **WHEN** an attempt is recorded, corrected, or removed while the tournament is in progress
- **THEN** the displayed ranking MUST refresh from backend data without requiring navigation away and back
- **AND** after completion the same leaderboard MUST remain visible as the final ranking

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

### Requirement: Leaderboard lifecycle and completed placements

A leaderboard tournament MUST be startable with zero participants, and starting MUST NOT expect generated matches. Completion MUST present the final leaderboard and placements returned by the backend, including guest participants and tied places. Resetting a leaderboard tournament MUST return the UI to a clean scheduled state without stale leaderboard participants, attempts, or placements. Lifecycle behavior for other bracket types MUST remain unchanged.

#### Scenario: Empty leaderboard starts

- **WHEN** an administrator starts a leaderboard tournament with no participants
- **THEN** the front end MUST NOT require registrations or participants before sending the start action
- **AND** the tournament MUST transition to the live leaderboard and result-entry state without expecting generated matches

#### Scenario: Completed leaderboard shows final placements

- **WHEN** an administrator completes a leaderboard tournament with at least one ranked participant
- **THEN** the final leaderboard and placement presentation MUST reflect the backend placements

#### Scenario: Guest participant wins

- **WHEN** a guest participant holds the winning best result at completion
- **THEN** the placement presentation MUST show the guest at the corresponding place
- **AND** the placement MUST NOT require a linked LAN account

#### Scenario: Tied placements are all rendered

- **WHEN** completion produces tied placements
- **THEN** every tied participant MUST be rendered at that shared place
- **AND** the presentation MUST NOT drop tied participants from the displayed result

#### Scenario: Completion is rejected without results

- **WHEN** the backend rejects completion because no participant has a valid recorded result
- **THEN** the front end MUST show the backend explanation
- **AND** it MUST remain in the in-progress leaderboard state

#### Scenario: Reset clears leaderboard state

- **WHEN** an administrator resets a completed or canceled leaderboard tournament
- **THEN** stale leaderboard participants, attempts, and placements MUST NOT remain displayed
- **AND** the tournament MUST be presented as a clean scheduled tournament

### Requirement: Leaderboard API boundary and mock parity

Leaderboard DTOs and `ILANClient` methods MUST match the backend contract for the anonymous tournament leaderboard read, the administrator leaderboard history read, and administrator attempt create, correction, and removal, in addition to the tournament-level ranking metric and bracket type. Mock backend mode MUST reproduce the same state semantics as the live API.

#### Scenario: Public leaderboard projection stays privacy-safe

- **WHEN** the anonymous leaderboard is rendered
- **THEN** only the metric, rank, participant display information, participant kind, and best result MAY be shown
- **AND** attempt history, concurrency tokens, and other administrator-only metadata MUST NOT be rendered

#### Scenario: Mock mode exercises the same leaderboard states

- **WHEN** mock backend mode is enabled
- **THEN** leaderboard configuration, result entry, correction, removal, lifecycle, and ranking MUST use the same state semantics as the live API

#### Scenario: Reload reproduces the leaderboard

- **WHEN** the tournament page is reloaded
- **THEN** the leaderboard MUST be reproduced from backend data
- **AND** front-end state MUST NOT be the source of truth for ranking or attempt history

#### Scenario: Leaderboard read fails

- **WHEN** the leaderboard cannot be loaded
- **THEN** the competition area MUST show an error or unavailable state with a recoverable next step
- **AND** it MUST NOT render a misleading empty leaderboard as if results did not exist

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

