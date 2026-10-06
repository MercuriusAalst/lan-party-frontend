## MODIFIED Requirements

### Requirement: Direct HTTP status responses render recovery pages

Direct HTTP requests that produce a forbidden or not-found response MUST render the matching branded recovery page during server-side rendering while preserving the original HTTP status code. A forbidden response MUST NOT render the protected route content. The forbidden recovery page MUST render inside the single application layout and MUST leave the Blazor circuit interactive, so the shared shell controls continue to work after the denial.

#### Scenario: Authenticated visitor requests a forbidden page

- **WHEN** an authenticated visitor requests a route their role cannot access
- **THEN** the response MUST retain HTTP 403
- **AND** the server-rendered page MUST show the existing forbidden message and safe navigation actions
- **AND** protected route content MUST NOT be rendered
- **AND** the page MUST NOT render a duplicate application layout

#### Scenario: Forbidden recovery page stays interactive

- **WHEN** an authenticated visitor reaches the forbidden recovery page through a denied direct request
- **THEN** the Blazor circuit MUST remain connected
- **AND** shared shell controls such as the theme toggle and navigation MUST still respond to interaction
- **AND** the interaction MUST NOT produce an unhandled circuit render exception

#### Scenario: Visitor requests an unknown route directly

- **WHEN** a visitor requests a route that does not exist
- **THEN** the response MUST retain HTTP 404
- **AND** the server-rendered page MUST show the existing not-found message and safe navigation actions
