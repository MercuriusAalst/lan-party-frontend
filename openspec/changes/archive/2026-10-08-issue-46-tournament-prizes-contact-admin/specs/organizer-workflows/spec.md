## ADDED Requirements

### Requirement: Admin tournament form composes contact administrator and prizes

The admin tournament create and edit surfaces SHALL allow an authorized administrator to select
or clear a contact administrator and to provide optional top-three prizes. The back end MUST
remain authoritative for administrator validity, prize limits, and validation failures.

#### Scenario: Admin selects a contact administrator

- **WHEN** an authorized administrator opens the tournament create or edit form and uses the
  contact-administrator picker
- **THEN** the picker MUST offer only validated administrators returned by the administrator list
  resource
- **AND** the picker MUST query that resource as the administrator types, reusing the existing
  search timing and cancellation behaviour
- **AND** it MUST keep the currently selected contact visible when the searched result list changes
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

#### Scenario: Administrator selection or save is rejected

- **WHEN** the backend rejects the selected administrator or a prize value, or the administrator
  list is unavailable
- **THEN** the form MUST keep the administrator's input and MUST present a recoverable message
- **AND** it MUST NOT present an optimistic success state
