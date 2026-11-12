## ADDED Requirements

### Requirement: Administrators can open team rosters from tournament participants

On a team tournament detail page, an administrator SHALL be able to open the roster of each active
team registration in a dialog from that team's card under Participants, following the existing
participant-dialog pattern. The roster MUST come from the administrator registration data the page
already loads, without a request per participant.

#### Scenario: Administrator opens a team roster

- **WHEN** an authenticated administrator activates a team participant card of a team tournament
  by pointer or keyboard
- **THEN** a modal dialog MUST open that names the team and lists its roster members by username
- **AND** the captain MUST be listed first and be identifiable
- **AND** each member's roster confirmation state MUST be shown as text, not by color alone
- **AND** no first name, last name, email, or linked platform ID MAY be rendered
- **AND** the dialog MUST trap focus and close with its close button, the Escape key, or a click
  outside the dialog

#### Scenario: Administrator opens player details from the roster dialog

- **WHEN** an administrator activates a member with a returned username in the roster dialog
- **THEN** the player details MUST be loaded from the administrator user endpoint and shown in the
  existing player details dialog without leaving the tournament page
- **AND** the player details dialog MUST offer a control that returns to the same team's roster
- **AND** if the details cannot be loaded, the roster dialog MUST stay open and an error MUST be shown
- **AND** the team name MUST link to `/teams/{teamName}` when a team name was returned

#### Scenario: Roster is not shown by default

- **WHEN** an administrator views the Participants section without activating a card
- **THEN** team participant cards MUST show only the team identity
- **AND** no roster member list MAY be rendered on the page itself

#### Scenario: Non-administrator views a team tournament

- **WHEN** an anonymous visitor or an authenticated user without the administrator role views the
  Participants section of a team tournament
- **THEN** team participant cards MUST NOT be interactive
- **AND** no roster dialog MAY be opened for any team

#### Scenario: Administrator registration data is not available

- **WHEN** the administrator registration list is still loading or failed to load
- **THEN** team participant cards MUST NOT be interactive
- **AND** an open roster dialog MUST close
- **AND** the page MUST NOT show a second error for the missing roster

#### Scenario: Team leaves the participants list

- **WHEN** the team whose roster dialog is open is no longer an active participant, for example
  after an administrator removes its registration
- **THEN** the roster dialog MUST close

#### Scenario: Administrator opens player details from an individual tournament

- **WHEN** an administrator activates a player card with a returned username in the Participants
  section of an individual tournament, by pointer or keyboard
- **THEN** the player details MUST be loaded from the administrator user endpoint and shown in the
  existing player details dialog without leaving the tournament page
- **AND** the dialog MUST NOT offer a return-to-roster control

#### Scenario: Non-administrator views an individual tournament

- **WHEN** an anonymous visitor or an authenticated user without the administrator role views the
  Participants section of an individual tournament
- **THEN** the player cards MUST remain non-interactive and no player details MAY be requested
