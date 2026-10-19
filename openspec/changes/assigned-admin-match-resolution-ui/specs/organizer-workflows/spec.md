## MODIFIED Requirements

### Requirement: Admin tournament form composes contact administrator and prizes

The admin tournament create and edit surfaces SHALL require an authorized administrator to select
an assigned administrator, who is also the tournament contact, and SHALL allow optional top-three
prizes. The assigned administrator MUST NOT be clearable. The back end MUST remain authoritative
for administrator validity, prize limits, and validation failures.

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
- **AND** the picker MUST NOT offer a choice that clears the assigned administrator
- **AND** the form MUST show a validation message and block saving until an administrator is selected
- **AND** it MUST keep the currently selected contact visible and selectable while the list loads,
  when the list is unavailable, and when the loaded list does not contain it
- **AND** selecting an administrator MUST mark that administrator as the tournament contact

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

#### Scenario: Admin edits a legacy tournament without an assigned administrator

- **WHEN** an authorized administrator opens the edit form for a tournament that has no assigned administrator
- **THEN** the form MUST prompt the administrator to assign one
- **AND** saving MUST stay blocked until an administrator is selected

## ADDED Requirements

### Requirement: Admins are warned when the assigned admin is no longer an administrator

The tournament overview MUST show a warning to administrators, and only to administrators, when the tournament's assigned admin is no longer an administrator, prompting a scheduled tournament's administrator to pick a replacement. The check MUST compare the identity of the tournament's contact administrator with the administrator list used by the admin picker, MUST NOT require a new endpoint, and MUST apply only to scheduled and in-progress tournaments. The public view, anonymous visitors and non-administrators MUST NOT see the warning or any hint of it and MUST NOT trigger the administrator list request. While the list is loading, when it fails to load, when it is empty, or when the tournament has no contact administrator, the overview MUST NOT show the warning.

#### Scenario: Assigned admin was revoked

- **WHEN** an administrator opens the overview of a scheduled or in-progress tournament whose contact administrator is not in the administrator list
- **THEN** the overview MUST show the warning that asks for a replacement

#### Scenario: In-progress tournament has no dead-end prompt

- **WHEN** the warning is shown for an in-progress tournament, which has no edit action
- **THEN** the text MUST state that the assigned admin is no longer an administrator and that matches awaiting resolution are therefore shown to all admins
- **AND** it MUST NOT ask the administrator to pick a replacement, and MUST state that no action is needed to unblock those matches
- **AND** a scheduled tournament MUST keep the text that asks for a replacement

#### Scenario: Assigned admin is still valid

- **WHEN** an administrator opens the overview and the contact administrator is in the administrator list
- **THEN** the overview MUST NOT show the warning

#### Scenario: Non-administrators never see the warning

- **WHEN** a non-administrator or anonymous visitor opens the overview
- **THEN** the warning MUST NOT be rendered
- **AND** the administrator list MUST NOT be requested

#### Scenario: Unverifiable state shows no false alarm

- **WHEN** the administrator list is loading, failed or empty
- **THEN** the overview MUST NOT show the warning
