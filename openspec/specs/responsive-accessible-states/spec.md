# responsive-accessible-states Specification

## Purpose
TBD - created by archiving change expand-product-redesign. Update Purpose after archive.
## Requirements
### Requirement: Responsive layouts preserve journey order

The front end SHALL adapt at the existing 640, 860, 960, 1100, and 1280 pixel
breakpoints so the primary heading, current state, primary action, and content
remain in a predictable reading and interaction order.

#### Scenario: Player page reaches a narrow viewport

- **WHEN** a player-facing page is rendered at or below a supported breakpoint
- **THEN** its sections MUST reflow into a readable column or bounded collection
- **AND** the primary action and state MUST remain visible before secondary detail
- **AND** the page MUST NOT create lateral document overflow

#### Scenario: Dense organizer or tournament content reaches mobile

- **WHEN** a bracket, schedule, roster, notification list, or organizer form is
  wider than the viewport
- **THEN** the dense surface MUST use an intentional stack or contained overflow
- **AND** it MUST not obscure adjacent actions or create unusable horizontal
  scrolling for the whole page

### Requirement: Interactive states are keyboard and screen-reader usable

The system SHALL ensure reorganized navigation, tabs, filters, dialogs, forms,
entity links, and action groups preserve logical keyboard order, visible focus,
accurate labels, expanded/controlled state, and live status announcements.

#### Scenario: Visitor operates a temporary navigation surface

- **WHEN** a visitor opens navigation, search, a dropdown, or a dialog using the
  keyboard
- **THEN** focus MUST remain in the active surface until selection or dismissal
- **AND** Escape, Enter, Arrow keys, and outside-click behavior MUST retain the
  existing supported semantics
- **AND** the control MUST expose its current expanded or selected state

#### Scenario: User submits an API-coupled form

- **WHEN** a user submits a registration, team, profile, sponsor, or organizer
  form
- **THEN** the pending, success, validation, unauthorized, conflict, or error
  state MUST be announced or associated with the relevant control
- **AND** field order and labels MUST remain understandable without a pointer

### Requirement: State surfaces remain visible and recoverable

Every reorganized journey MUST retain visible loading, empty, error, unauthorized,
forbidden, not-found, conflict, and success/confirmation states and MUST keep
existing recovery actions and message meaning.

#### Scenario: Data request is unavailable

- **WHEN** a public, player, or organizer request returns empty, unauthorized,
  forbidden, not-found, conflict, or error
- **THEN** the UI MUST identify the state in the affected section
- **AND** it MUST preserve a retry, sign-in, back, or known-destination action
  whenever the current flow provides one

### Requirement: Motion, theme, and contrast remain preference-aware

The redesign MUST retain reduced-motion support, avoid decorative motion as the
only way to communicate state, and maintain WCAG AA contrast for both the
user-selected light and dark themes and existing logo assets. Dark mode MUST
use neutral surfaces and off-white text rather than green-tinted general copy.
Contrast corrections MUST be made at the owning selector or semantic token;
broad global recoloring and broad `!important` overrides are not permitted.

#### Scenario: Visitor prefers reduced motion

- **WHEN** the browser reports `prefers-reduced-motion: reduce`
- **THEN** non-essential transition, scroll, and decorative animation
  MUST be disabled or reduced
- **AND** content and action availability MUST remain unchanged

#### Scenario: Header and footer render logos

- **WHEN** the shell renders an existing logo asset on its selected dark or light
  surface
- **THEN** the chosen asset MUST remain distinguishable from its background
- **AND** the logo MUST retain its accessible alternative text

#### Scenario: User opens an information popup

- **WHEN** a user selects a member or participant identity
- **THEN** the popup MUST fit the viewport without page-wide overflow and expose
  an accessible name, close action, and visible focus
- **AND** Escape and focus restoration MUST work in both themes

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

### Requirement: Boolean accessibility states serialize explicitly

Controls that expose boolean `aria-expanded`, `aria-hidden`, `aria-selected`, or `aria-pressed` state MUST render the literal values `true` or `false` that match their current state during server rendering and after interaction.

#### Scenario: Visitor views a boolean-state control

- **WHEN** a control exposes its expanded, hidden, selected, or pressed state
- **THEN** its corresponding ARIA attribute MUST contain the matching literal boolean value
- **AND** the attribute MUST NOT be rendered as an empty value for either state

