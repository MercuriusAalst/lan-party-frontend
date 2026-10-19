## ADDED Requirements

### Requirement: Info packing list hardware group uses a neutral accent

The Info packing list MUST render the hardware group with a neutral slate accent and neutral tint instead of green in both light and dark themes. The basic and optional groups MUST keep their distinct orange and blue accents so the three priority groups remain visually distinguishable. Ticket names MUST remain in the main theme green.

#### Scenario: Visitor views the packing list

- **WHEN** the Info packing list renders in light or dark theme
- **THEN** hardware items MUST use a neutral slate accent, icon tint and group label without green
- **AND** hardware, basic and optional items MUST remain distinguishable and their text MUST meet WCAG AA contrast
