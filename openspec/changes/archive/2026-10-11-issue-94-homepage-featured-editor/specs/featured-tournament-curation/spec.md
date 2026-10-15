## MODIFIED Requirements

### Requirement: Administrators curate the featured homepage tournaments

The homepage featured section MUST expose an administrator-only **Edit** action
that opens the featured editor inline, in place of the read-only cards. The
editor MUST preview the public arrangement of one large lead card followed by
three smaller cards, and it MUST stay unavailable to anonymous visitors and
non-administrators.

#### Scenario: Administrator opens the inline featured editor

- **WHEN** an authenticated administrator activates **Edit** in the homepage
  featured section
- **THEN** the section MUST replace the read-only cards with the editor for the
  current featured arrangement as one lead card followed by three secondary
  cards in display order
- **AND** the editor MUST be reachable from the homepage itself, without a
  separate featured-tournaments page

#### Scenario: Non-administrator cannot reach the editor

- **WHEN** an anonymous visitor or an authenticated non-administrator loads the
  homepage
- **THEN** the section MUST NOT present the **Edit** action or any editor
  control
- **AND** saving MUST require the existing administrator authorization, so an
  unauthorized save attempt MUST NOT change the stored selection

### Requirement: Unsaved editor changes stay local until a save succeeds

Editing the arrangement MUST only affect the local inline draft. The public
cards and the hero MUST change only after the save operation has been confirmed.
**Cancel** MUST discard the draft, and a rejected save MUST keep the draft and
the saved baseline unchanged while showing a recoverable message. The editor
MUST guard its controls while a load or save is in flight.

#### Scenario: Administrator previews without saving

- **WHEN** an administrator reorders, adds, or removes tournaments in the
  inline editor without saving
- **THEN** the draft MUST reflect the local arrangement
- **AND** the homepage cards and the hero MUST still render the previously saved
  arrangement

#### Scenario: Administrator cancels the draft

- **WHEN** an administrator activates **Cancel**
- **THEN** the editor MUST close without sending a save request
- **AND** the homepage MUST render the previously saved arrangement

#### Scenario: Save succeeds

- **WHEN** an administrator confirms a valid save
- **THEN** the homepage cards and the hero MUST render the server-returned order
  without a page reload

#### Scenario: Save fails

- **WHEN** the save operation is rejected with validation, unauthorized,
  forbidden, or transport failure
- **THEN** the editor MUST keep the administrator's edits visible with a
  recoverable message
- **AND** it MUST NOT replace the saved baseline with the rejected arrangement

#### Scenario: A featured read that lands after the save cannot restore the old order

- **WHEN** the homepage featured read is still in flight and an administrator
  confirms a save before that read completes
- **THEN** the completed read MUST NOT replace the server-confirmed order on the
  featured cards or the hero
- **AND** the section MUST stop showing its featured loading state once the save
  is confirmed

## ADDED Requirements

### Requirement: The inline editor reorders by dragging the cards

The editor MUST reorder the four positions by dragging the rendered cards,
including dragging across the lead card and the secondary rows, and it MUST
offer a non-drag position control for keyboard and single-pointer users. While
the editor is open, the cards MUST NOT navigate to a tournament.

#### Scenario: Administrator drags a card to another position

- **WHEN** an administrator drags a card and drops it on another lead or
  secondary position
- **THEN** the draft MUST adopt that order
- **AND** the dragged card and the active drop position MUST be visually
  indicated

#### Scenario: Administrator reorders without dragging

- **WHEN** an administrator uses the compact position control on a card
- **THEN** the draft MUST move that tournament to the chosen position

#### Scenario: Editor cards do not navigate

- **WHEN** an administrator activates a card while the editor is open
- **THEN** the application MUST NOT navigate to the tournament detail route

### Requirement: The inline editor adds and removes tournaments

The editor MUST offer the remaining eligible tournaments for adding and MUST
offer removal for every filled position. The existing paginated eligible
inventory and the existing exactly-four-distinct-eligible validation MUST be
reused.

#### Scenario: Administrator adds and removes tournaments

- **WHEN** an administrator adds an available eligible tournament or removes a
  selected one
- **THEN** the draft MUST reflect the change
- **AND** saving MUST stay unavailable until exactly four distinct eligible
  tournaments are selected
