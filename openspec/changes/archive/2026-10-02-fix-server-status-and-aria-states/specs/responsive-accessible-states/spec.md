## ADDED Requirements

### Requirement: Direct HTTP status responses render recovery pages

Direct HTTP requests that produce a forbidden or not-found response MUST render the matching branded recovery page during server-side rendering while preserving the original HTTP status code. A forbidden response MUST NOT render the protected route content.

#### Scenario: Authenticated visitor requests a forbidden page

- **WHEN** an authenticated visitor requests a route their role cannot access
- **THEN** the response MUST retain HTTP 403
- **AND** the server-rendered page MUST show the existing forbidden message and safe navigation actions
- **AND** protected route content MUST NOT be rendered

#### Scenario: Visitor requests an unknown route directly

- **WHEN** a visitor requests a route that does not exist
- **THEN** the response MUST retain HTTP 404
- **AND** the server-rendered page MUST show the existing not-found message and safe navigation actions

### Requirement: Boolean accessibility states serialize explicitly

Controls that expose boolean `aria-expanded`, `aria-hidden`, `aria-selected`, or `aria-pressed` state MUST render the literal values `true` or `false` that match their current state during server rendering and after interaction.

#### Scenario: Visitor views a boolean-state control

- **WHEN** a control exposes its expanded, hidden, selected, or pressed state
- **THEN** its corresponding ARIA attribute MUST contain the matching literal boolean value
- **AND** the attribute MUST NOT be rendered as an empty value for either state
