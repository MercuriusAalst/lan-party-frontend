# organizer-workflows Specification

## Purpose
TBD - created by archiving change expand-product-redesign. Update Purpose after archive.
## Requirements
### Requirement: Organizer tools are grouped by authorized task

The front end SHALL group existing organizer capabilities around tournament
setup, schedule and lifecycle, registration oversight, sponsor placement, and
team administration without exposing those controls to users lacking the
existing administrator role.

#### Scenario: Admin opens organizer tools

- **WHEN** an authenticated administrator opens an organizer-facing route or
  menu
- **THEN** the UI MUST present the existing authorized tournament, sponsor,
  lifecycle, registration, and team-management entry points in task-oriented
  groups
- **AND** each entry point MUST retain its existing route and callback

#### Scenario: Non-admin views the same navigation

- **WHEN** an authenticated non-admin or anonymous visitor opens the shell
- **THEN** administrator-only entries and controls MUST remain hidden or
  unauthorized according to the existing authorization behavior
- **AND** public/player navigation MUST remain usable

### Requirement: Organizer mutations remain backend-authoritative

Organizer composition MUST preserve the current tournament create/update/delete,
lifecycle, sponsor placement, registration removal, team, and profile mutation
contracts and MUST refresh from the confirmed response before presenting the new
state.

#### Scenario: Admin submits an organizer mutation

- **WHEN** an authorized administrator confirms an existing organizer action
- **THEN** the front end MUST call the current service/API contract with the same
  request shape
- **AND** it MUST show the existing success feedback only after the mutation and
  authoritative refresh succeed

#### Scenario: Organizer mutation fails

- **WHEN** the backend returns validation, conflict, unauthorized, forbidden,
  not-found, or generic failure feedback
- **THEN** the UI MUST preserve the current data projection
- **AND** it MUST expose a recoverable message without optimistic mutation state

### Requirement: Organizer workflows preserve mock/live parity

Organizer task composition SHALL use the same service-level semantics in mock and
live modes, including representative loading, empty, success, blocked, conflict,
and error states.

#### Scenario: Admin validates a workflow in mock mode

- **WHEN** mock backend mode is enabled and an administrator exercises an
  organizer task
- **THEN** the UI MUST use the existing mock service behavior and response shapes
- **AND** the journey MUST remain usable without a live-only endpoint or field

### Requirement: Sponsor context stays subordinate to event tasks

Sponsor discovery and sponsor administration SHALL remain distinct: public
sponsor content is discoverable for visitors, while sponsor mutation controls
remain in the authorized organizer context.

#### Scenario: Visitor views sponsors

- **WHEN** a public visitor opens sponsor content
- **THEN** the page MUST show public sponsor information and configured links
- **AND** no sponsor mutation control or private organizer data MUST be shown

#### Scenario: Admin manages sponsor placement

- **WHEN** an authorized administrator edits an existing sponsor placement
- **THEN** the admin surface MUST retain the current zero-or-one placement
  semantics and refresh behavior
- **AND** the public sponsor presentation MUST update only from confirmed state

### Requirement: Tournament lifecycle actions reflect backend eligibility

The tournament detail UI MUST show or disable administrator lifecycle actions according to the tournament's current status and available prerequisite data. Backend validation MUST remain authoritative.

#### Scenario: Admin starts a scheduled tournament

- **WHEN** an administrator views a scheduled tournament
- **THEN** the start action MUST be unavailable for unsupported Swiss brackets
- **AND** a non-leaderboard start action MUST be disabled until at least two active registrations match the tournament participation mode
- **AND** a leaderboard start action MUST NOT require registrations

#### Scenario: Admin cancels a tournament

- **WHEN** an administrator views a tournament that is not completed or already canceled
- **THEN** the cancel action MUST be available
- **AND** the cancel action MUST be hidden when the tournament is completed or already canceled

#### Scenario: Admin finishes a tournament

- **WHEN** an administrator views an in-progress tournament
- **THEN** the finish action MUST be available for non-leaderboard brackets
- **AND** the finish action for a leaderboard tournament MUST be disabled until the public leaderboard contains a recorded result
- **AND** the finish action MUST be hidden when the tournament is not in progress

#### Scenario: Admin resets a tournament

- **WHEN** an administrator views a completed or canceled tournament
- **THEN** the reset action MUST be available
- **AND** the reset action MUST be hidden for scheduled or in-progress tournaments

#### Scenario: Admin deletes an in-progress tournament

- **WHEN** an administrator views an in-progress tournament
- **THEN** the delete action MUST be disabled because the backend rejects deletion in that state

#### Scenario: Backend rejects a lifecycle request

- **WHEN** a lifecycle request fails with backend problem details
- **THEN** the UI MUST show the available backend explanation, with a localized fallback when no explanation is present
- **AND** after a validation or concurrency failure the UI MUST refresh tournament state before presenting the actions again

### Requirement: Admin tournament form composes contact administrator and prizes

The admin tournament create and edit surfaces SHALL allow an authorized administrator to select
or clear a contact administrator and to provide optional top-three prizes. The back end MUST
remain authoritative for administrator validity, prize limits, and validation failures.

#### Scenario: Admin selects a contact administrator

- **WHEN** an authorized administrator opens the tournament create or edit form and uses the
  contact-administrator picker
- **THEN** the picker MUST offer only validated administrators returned by the administrator list
  resource
- **AND** the picker MUST load the complete administrator list when the form opens instead of
  querying as the administrator types
- **AND** the picker MUST page through the administrator list until the last page rather than
  presenting a silently partial list
- **AND** the picker MUST NOT require or accept free-text input
- **AND** the picker MUST always offer an explicit choice that clears the contact administrator
- **AND** it MUST keep the currently selected contact visible and selectable while the list loads,
  when the list is unavailable, and when the loaded list does not contain it
- **AND** selecting an administrator MUST mark that administrator as the tournament contact
- **AND** the admin MUST be able to clear an already selected contact before saving

#### Scenario: Admin edits a tournament with an existing contact administrator

- **WHEN** an authorized administrator opens the edit form for a tournament that already has a
  contact administrator
- **THEN** the picker MUST be initialized from the tournament's existing public contact
  projection
- **AND** saving without changing the picker MUST preserve the existing contact

#### Scenario: Admin provides optional prizes

- **WHEN** an authorized administrator provides first, second, or third place prize values
- **THEN** the form MUST send only the prizes that were provided, with surrounding whitespace
  removed
- **AND** leaving a prize empty MUST send an explicit empty field so the backend clears it

#### Scenario: Administrator list is unavailable

- **WHEN** the administrator list resource fails while the picker is loading
- **THEN** the picker MUST present a recoverable, accessible error state
- **AND** it MUST NOT present a silently partial administrator list

#### Scenario: Administrator selection or save is rejected

- **WHEN** the backend rejects the selected administrator or a prize value, or the administrator
  list is unavailable
- **THEN** the form MUST keep the administrator's input and MUST present a recoverable message
- **AND** it MUST NOT present an optimistic success state

