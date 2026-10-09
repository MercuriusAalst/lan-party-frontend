# featured-tournament-curation Specification

## Purpose
TBD - created by archiving change issue-94-featured-homepage-tournaments. Update Purpose after archive.
## Requirements
### Requirement: Administrators curate the featured homepage tournaments

The front end MUST expose an administrator-only featured-tournament editor that
previews the public homepage arrangement of one large lead card followed by
three smaller cards, and it MUST keep that editor unavailable to anonymous and
non-administrator users.

#### Scenario: Administrator opens the featured editor

- **WHEN** an authenticated administrator opens the featured tournaments editor
- **THEN** the page MUST show the current featured arrangement as one lead card
  followed by three secondary cards in display order
- **AND** the page MUST be reachable from the existing organizer navigation

#### Scenario: Non-administrator requests the featured editor

- **WHEN** an anonymous visitor or an authenticated non-administrator opens the
  featured tournaments editor route
- **THEN** the page MUST NOT present the editor or its save action
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

#### Scenario: Featured read fails

- **WHEN** the featured read fails
- **THEN** the homepage MUST show the existing recoverable error and retry state
- **AND** the rest of the page MUST remain usable

### Requirement: Unsaved editor changes stay local until a save succeeds

Rearranging or replacing tournaments in the editor MUST only affect the local
preview. The public homepage MUST change only after the save operation has been
confirmed.

#### Scenario: Administrator previews without saving

- **WHEN** an administrator reorders or replaces tournaments without saving
- **THEN** the preview MUST reflect the local arrangement
- **AND** the public homepage MUST still render the previously saved arrangement

#### Scenario: Save fails

- **WHEN** the save operation is rejected with validation, unauthorized,
  forbidden, or transport failure
- **THEN** the editor MUST keep the administrator's edits visible with a
  recoverable message
- **AND** it MUST NOT replace the saved baseline with the rejected arrangement

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

