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

### Requirement: Button states stay on the app palette

Controls that combine the Bootstrap `btn` class with an app button variant (primary, secondary, outline) MUST render their hover, keyboard focus, active and disabled states from the app's button tokens. Keyboard focus MUST keep the variant's neutral or brand background and text and add only a visible focus border or ring, and disabled controls MUST keep the variant's own treatment at reduced opacity. These states MUST NOT render Bootstrap's default blue or grey.

#### Scenario: Keyboard focus lands on a Bootstrap-class secondary button

- **WHEN** a control with both `btn` and `btn-secondary` or `btn-outline-*` receives keyboard focus or is disabled
- **THEN** it MUST keep the neutral secondary background and ink text with a visible focus ring
- **AND** it MUST NOT show a Bootstrap blue or grey state

### Requirement: Footer groups the information links under Event Info

The site footer MUST present the When & Where, Tickets, Catering and Contact links under a single Event Info heading, linking to the existing Info page sections, and MUST NOT render a separate Explore heading or the former descriptive and offline-mock paragraphs. The footer grid MUST NOT leave an empty column or gap at desktop, tablet or mobile widths.

#### Scenario: Visitor reads the footer

- **WHEN** a visitor views the site footer
- **THEN** the Event Info heading MUST contain the When & Where, Tickets, Catering and Contact links
- **AND** no Explore heading or offline mock mode text MUST be shown
