# featured-tournament-curation Specification

## Purpose
TBD - created by archiving change issue-94-featured-homepage-tournaments. Update Purpose after archive.
## Requirements
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

### Requirement: The homepage renders the saved curated order

The homepage MUST render the server-ordered featured tournaments as one large
lead card followed by three smaller cards, and it MUST be able to render a
curated tournament that is not part of the first page of the public tournament
listing without loading full tournament graphs.

#### Scenario: Visitor loads the homepage with a saved selection

- **WHEN** an anonymous visitor opens the homepage and a featured selection was
  saved
- **THEN** the lead card MUST show the first saved tournament
- **AND** the remaining saved tournaments MUST follow in the configured order as
  secondary cards
- **AND** selecting a card MUST navigate to the canonical GUID tournament route

#### Scenario: A curated tournament is outside the first public page

- **WHEN** the saved selection contains a tournament that is not in the first
  page of the public tournament listing
- **THEN** the homepage MUST still render that tournament from the minimal
  featured summary projection
- **AND** it MUST NOT load that tournament's registrations, rosters, matches,
  placements, or leaderboard entries just to render the card

#### Scenario: Featured cards show the public sponsor attribution

- **WHEN** a featured tournament has a public sponsor placement with a sponsor
  name
- **THEN** its lead or row card MUST show the localized "Presented by
  <sponsor>" attribution
- **AND** the card MUST omit the attribution when the placement or sponsor name
  is absent or blank
- **AND** a long sponsor name MUST stay in full and wrap instead of being
  truncated

#### Scenario: Featured read fails

- **WHEN** the featured read fails
- **THEN** the homepage MUST show the existing recoverable error and retry state
- **AND** the rest of the page MUST remain usable

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

### Requirement: Default ordering and invalidated selections degrade safely

The homepage MUST keep the previous default behavior before any selection was
ever saved and MUST recover without duplicates when a saved tournament is no
longer eligible.

#### Scenario: No selection was ever saved

- **WHEN** no featured selection has ever been saved
- **THEN** the homepage MUST fall back to the first eligible tournaments in the
  existing default order

#### Scenario: A saved tournament is no longer eligible

- **WHEN** a saved tournament is deleted or canceled
- **THEN** the remaining valid selections MUST keep their configured relative
  order
- **AND** the gaps MUST be filled from other eligible tournaments without
  duplicates, reaching four whenever four eligible tournaments exist
- **AND** an ineligible tournament MUST NOT be displayed

#### Scenario: Fewer than four eligible tournaments exist

- **WHEN** fewer than four eligible tournaments exist
- **THEN** the homepage MUST render the available eligible tournaments
  gracefully instead of failing

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

