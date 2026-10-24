## ADDED Requirements

### Requirement: Inline tournament scroll regions chain to the page

Inline tournament participant and schedule lists MUST scroll internally when
their content overflows and MUST allow vertical wheel and trackpad input to
continue scrolling the document when the list reaches its boundary.

#### Scenario: Visitor scrolls over an empty or overflowing tournament list

- **WHEN** a visitor uses vertical wheel input over an inline tournament
  participant or schedule list
- **THEN** input MUST scroll the list or document in the requested direction
  while scrolling can continue
- **AND** input MUST continue scrolling the document at the list boundary,
  including when the list has no overflow

#### Scenario: Visitor opens registration content

- **WHEN** a visitor opens a registration dialog
- **THEN** the dialog MUST retain its existing scroll behavior
