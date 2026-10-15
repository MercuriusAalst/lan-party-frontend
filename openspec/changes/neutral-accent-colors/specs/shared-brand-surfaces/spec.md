## ADDED Requirements

### Requirement: Informational badges and secondary buttons use neutral accents

Shared informational badges MUST use a neutral background, a neutral border and neutral ink text in both light and dark themes, and MUST meet WCAG AA contrast. Status pills that convey a tournament or registration status MUST keep their semantic status colours. Secondary and outline buttons MUST render their text in the neutral ink colour with a neutral border; the brand green MAY appear on their hover and focus borders only. Primary buttons MUST keep the brand green. The global link colour MUST NOT change.

#### Scenario: Tournament card shows informational badges

- **WHEN** a tournament card or tournament detail view renders participation, bracket, format or round badges
- **THEN** the badges MUST render with neutral surface, border and text colours
- **AND** the tournament status pill MUST keep its semantic status colour

#### Scenario: Secondary button is rendered

- **WHEN** a secondary or outline button is rendered in either theme
- **THEN** its text MUST use the neutral ink colour and its border MUST be neutral
- **AND** a primary button on the same page MUST remain brand green
