## REMOVED Requirements

### Requirement: Exactly four distinct eligible tournaments are persisted

The featured selection MUST persist as exactly four distinct tournament
identifiers. A tournament is eligible only when it still exists and its status is
not canceled. Duplicate identifiers and ineligible tournaments MUST NOT be
saved, and the front end MUST validate the selection before saving while the
backend remains authoritative.

#### Scenario: Administrator saves four distinct selections

- **WHEN** an administrator has selected four distinct eligible tournaments and
  confirms **Save changes**
- **THEN** the front end MUST send exactly those four identifiers in display
  order to the featured save operation
- **AND** it MUST adopt the confirmed response as the new saved arrangement

#### Scenario: Fewer than four selections cannot be saved

- **WHEN** fewer than four positions are filled
- **THEN** the front end MUST show a validation message naming the remaining
  requirement
- **AND** it MUST NOT send a save request

#### Scenario: Fewer than four eligible tournaments exist

- **WHEN** fewer than four eligible tournaments exist globally
- **THEN** the editor MUST explain that four eligible tournaments are required
  and show how many are currently available
- **AND** saving an incomplete selection MUST stay unavailable

#### Scenario: Duplicate or ineligible selections are rejected

- **WHEN** a selection would repeat an already selected tournament or include a
  canceled tournament
- **THEN** the editor MUST prevent the duplicate or ineligible selection
- **AND** a rejected save MUST leave the previously saved arrangement intact

#### Scenario: A previously saved tournament is no longer eligible

- **WHEN** a saved featured tournament was deleted or canceled since it was
  saved
- **THEN** the editor MUST NOT present that tournament as a filled position
- **AND** the save action MUST stay unavailable until four distinct eligible
  tournaments are selected
- **AND** a save attempt that still carries an ineligible position MUST NOT
  reach the backend

## ADDED Requirements

### Requirement: One to four distinct eligible tournaments are persisted

The featured selection MUST persist as one to four distinct tournament
identifiers, in curated display order. A tournament is eligible only when it
still exists and its status is not canceled. An empty selection, more than four
identifiers, duplicate identifiers, and ineligible tournaments MUST NOT be
saved. The front end MUST validate the selection before saving while the backend
remains authoritative.

#### Scenario: Administrator saves one to four distinct selections

- **WHEN** an administrator has selected one, two, three, or four distinct
  eligible tournaments and confirms **Save changes**
- **THEN** the front end MUST send exactly those identifiers, in display order,
  to the featured save operation
- **AND** it MUST adopt the confirmed response as the new saved arrangement
- **AND** the stored selection MUST keep the submitted count instead of being
  padded back to four

#### Scenario: An empty selection cannot be saved

- **WHEN** no tournament is selected in the draft
- **THEN** the front end MUST show a localized minimum-one validation message
  and MUST disable **Save changes**
- **AND** it MUST NOT send a save request

#### Scenario: A fifth tournament cannot be added

- **WHEN** four tournaments are already selected
- **THEN** the editor MUST refuse to add another tournament to the draft

#### Scenario: Duplicate or ineligible selections are rejected

- **WHEN** a selection would repeat an already selected tournament or include a
  canceled tournament
- **THEN** the editor MUST prevent the duplicate or ineligible selection
- **AND** a rejected save MUST leave the previously saved arrangement intact
- **AND** a save attempt that still carries an ineligible position MUST NOT
  reach the backend

#### Scenario: A previously saved tournament is no longer eligible

- **WHEN** a saved featured tournament was deleted or canceled since it was
  saved
- **THEN** the editor MUST NOT present that tournament as a selected card
- **AND** it MUST keep the remaining valid selections as the draft
- **AND** it MUST stay saveable while at least one valid selection remains

## MODIFIED Requirements

### Requirement: Administrators curate the featured homepage tournaments

The homepage featured section MUST expose an administrator-only **Edit** action
that opens the featured editor inline, in place of the read-only cards. The
editor MUST preview the public arrangement of one large lead card followed by up
to three smaller cards, and it MUST stay unavailable to anonymous visitors and
non-administrators.

#### Scenario: Administrator opens the inline featured editor

- **WHEN** an authenticated administrator activates **Edit** in the homepage
  featured section
- **THEN** the section MUST replace the read-only cards with the editor for the
  current featured arrangement as one lead card followed by up to three
  secondary cards in display order
- **AND** the editor MUST be reachable from the homepage itself, without a
  separate featured-tournaments page

#### Scenario: Non-administrator cannot reach the editor

- **WHEN** an anonymous visitor or an authenticated non-administrator loads the
  homepage
- **THEN** the section MUST NOT present the **Edit** action or any editor
  control
- **AND** saving MUST require the existing administrator authorization, so an
  unauthorized save attempt MUST NOT change the stored selection

### Requirement: The inline editor reorders by dragging the cards

The editor MUST reorder the selected cards by dragging any of them, including
dragging across the lead card and the secondary rows, and it MUST offer a
non-drag position control for keyboard and single-pointer users. It MUST stay
error-free with a single card or with two cards. While the editor is open, the
cards MUST NOT navigate to a tournament.

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
offer removal for every selected card. The existing paginated eligible inventory
and the one-to-four distinct-eligible validation MUST be reused.

#### Scenario: Administrator adds and removes tournaments

- **WHEN** an administrator adds an available eligible tournament or removes a
  selected one
- **THEN** the draft MUST reflect the change
- **AND** saving MUST stay available while at least one and at most four
  distinct eligible tournaments are selected

#### Scenario: Administrator removes a card from the middle of the draft

- **WHEN** an administrator removes a card that is not the last one
- **THEN** the remaining selected cards MUST close the gap instead of leaving an
  empty position behind
- **AND** the first remaining card MUST become the lead card

#### Scenario: Fewer than four eligible tournaments exist

- **WHEN** fewer than four eligible tournaments exist globally
- **THEN** the editor MUST still allow saving the eligible tournaments that are
  selected
- **AND** it MUST only explain the missing inventory when no eligible tournament
  exists at all

### Requirement: Default ordering and invalidated selections degrade safely

The homepage MUST keep the previous default behavior before any selection was
ever saved and MUST recover without duplicates when a saved tournament is no
longer eligible.

#### Scenario: No selection was ever saved

- **WHEN** no featured selection has ever been saved
- **THEN** the homepage MUST fall back to the first eligible tournaments in the
  existing default order, up to four of them

#### Scenario: A saved tournament is no longer eligible

- **WHEN** a saved tournament is deleted or canceled
- **THEN** the remaining valid selections MUST keep their configured relative
  order
- **AND** the gaps MUST be filled from other eligible tournaments without
  duplicates, up to the saved selection count and never beyond it
- **AND** an ineligible tournament MUST NOT be displayed

#### Scenario: Fewer than four eligible tournaments exist

- **WHEN** fewer than four eligible tournaments exist
- **THEN** the homepage MUST render the available eligible tournaments
  gracefully instead of failing
