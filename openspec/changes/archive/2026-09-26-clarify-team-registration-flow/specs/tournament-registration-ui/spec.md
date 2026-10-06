## MODIFIED Requirements

### Requirement: Tournament registration is an accessible popup workflow

The tournament detail page MUST expose a concise registration trigger and render the registration
workflow in an accessible modal popup rather than inline in the page content.

#### Scenario: Visitor opens registration

- **WHEN** a visitor selects the tournament registration action
- **THEN** the workflow MUST open in a labelled dialog with a clear close action
- **AND** keyboard focus MUST move into the dialog, remain usable within it, and return to the
  trigger after dismissal
- **AND** the underlying tournament page MUST not render a second inline registration component

#### Scenario: Captain selects a team

- **WHEN** an eligible captain selects a team in the team-registration popup
- **THEN** the popup MUST identify whether the captain is registering the team or editing its
  existing registration
- **AND** the selected team MUST be visibly and accessibly identified
- **AND** the popup MUST remain on team selection until the captain chooses an action labelled for
  the next step, such as `Choose roster` or `Edit roster`
- **AND** an unavailable team MUST stay visibly status-marked with the visual label `Unavailable`
- **AND** validation reasons, raw reason codes, and explanatory paragraphs MUST not be rendered
  beside the option

#### Scenario: Captain edits the selected roster

- **WHEN** the captain advances from team selection to roster selection
- **THEN** the next action MUST be labelled `Review roster`
- **AND** the final action MUST clearly indicate whether it submits a new registration or saves an
  edited roster
- **AND** a registration-cancellation action MUST remain visually secondary to the forward action

#### Scenario: Selected player confirms a roster invitation

- **WHEN** a player has a pending roster invitation for the tournament
- **THEN** the popup MUST show that player's Accept and Decline decision without presenting the
  captain's team-selection or roster-edit workflow

#### Scenario: Eligible player registers individually

- **WHEN** an eligible authenticated player selects the individual registration action
- **THEN** the UI MUST use a concise action label without restating that the player is an individual
- **AND** the next interaction MUST be a friendly confirmation describing the tournament outcome
  before the existing mutation is sent

#### Scenario: User sees an unavailable roster option

- **WHEN** a team or roster member cannot be used for the tournament
- **THEN** the UI MUST communicate unavailability through disabled/status styling and an icon or
  label, not explanatory validation text
- **AND** the accessible label MUST communicate `Unavailable` without exposing raw backend reason
  codes or technical refresh terminology

#### Scenario: Unavailable roster members remain visible but excluded

- **WHEN** a roster member is marked unavailable or ineligible for the tournament
- **THEN** the member MUST remain visible in the roster selection
- **AND** the member's selection control MUST initialize unchecked and render as
  non-selectable/disabled with the accessible state `Unavailable`
- **AND** the member MUST be excluded from any restored or realtime-preserved roster draft
- **AND** the member MUST be excluded from the submitted roster even if a stale draft or client
  state previously contained that member
- **AND** eligible roster members MUST retain their existing selectable and checked-state semantics
- **AND** captain and team selection semantics MUST remain unchanged
