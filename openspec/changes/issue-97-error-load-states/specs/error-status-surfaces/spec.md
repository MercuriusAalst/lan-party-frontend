## ADDED Requirements

### Requirement: Failed page loads use the shared status surface

Every full-page load failure, unavailable-service, forbidden, not-found, and unexpected-server-failure state MUST render through the shared `StatusPage` component instead of page-specific error markup. The shared surface MUST present a contextual eyebrow, a human-readable title that names the state, a short non-technical explanation, and a grouped action area.

#### Scenario: Tournament detail fails to load

- **WHEN** the tournament detail request fails and no tournament data is available
- **THEN** the page MUST render the shared status surface
- **AND** the rendered heading MUST identify the failure rather than presenting loading copy
- **AND** the surface MUST offer a retry action and a back-to-tournaments action

#### Scenario: Tournament detail route cannot be resolved

- **WHEN** the requested tournament does not exist
- **THEN** the page MUST render the shared not-found variant
- **AND** the surface MUST offer a back-to-tournaments action

#### Scenario: A page-specific error structure is still hand-rolled

- **WHEN** a page renders a full-page failure state
- **THEN** the page MUST reuse the shared status surface rather than duplicating its markup

### Requirement: Status surface variants are distinct and copy is localised

The shared status surface MUST distinguish loading, empty, no-results, not-found, authorization-failure, and transient-service-failure states. Each state MUST use a stable, non-technical message and MUST NOT expose exception details. All status copy MUST be available in both `en-US` and `nl-BE`.

#### Scenario: Visitor reads a status surface in either supported language

- **WHEN** the active language is `en-US` or `nl-BE`
- **THEN** the status title, explanation, and action labels MUST render in that language
- **AND** no untranslated placeholder or raw exception text MUST be shown

#### Scenario: Loading state precedes a resolved load

- **WHEN** a page request is still in flight
- **THEN** the page MUST show the loading presentation
- **AND** it MUST NOT show a failure title or failure actions

### Requirement: Status surfaces recover in place or navigate deliberately

When a retry is meaningful, the shared status surface MUST offer a retry action that reruns the page's initial load without a forced browser reload. Navigation actions MUST remain available as links to known destinations.

#### Scenario: Service recovers while a failure state is shown

- **WHEN** a page displays a transient failure status surface and the request would now succeed
- **AND** the user selects the retry action
- **THEN** the page MUST rerun its initial load in place
- **AND** the page MUST render its loaded content when the request succeeds

#### Scenario: Retry is pending

- **WHEN** the user selects the retry action
- **THEN** the surface MUST indicate a pending or loading state
- **AND** it MUST NOT accept the same retry action a second time

### Requirement: Inline section failures stay compact and non-destructive

Failures of an independently loaded widget, tab, sponsor section, search result, or match-summary section MUST render a compact contextual message inside that section. A section failure MUST NOT replace, hide, or block content that loaded successfully elsewhere on the page.

#### Scenario: One page section fails while others succeed

- **WHEN** one section's request fails and another independent request succeeds
- **THEN** the failed section MUST show a compact inline unavailable message
- **AND** the successfully loaded sections MUST remain visible and usable

#### Scenario: Homepage sponsor data is unavailable

- **WHEN** sponsor data cannot be loaded on a page that loaded other content
- **THEN** the sponsor error MUST stay inside the sponsor area
- **AND** the rest of the page MUST remain usable

### Requirement: Status surfaces are accessible, responsive, and legible

The shared status surface MUST expose its state as an announced status or alert region, keep a single descriptive page heading, keep action controls reachable and operable in logical keyboard order, and preserve WCAG AA contrast. The composition MUST remain readable without lateral overflow from mobile widths up to wide desktop widths, and text width MUST stay constrained.

#### Scenario: Keyboard-only visitor recovers from a failed load

- **WHEN** the status surface is displayed
- **THEN** the retry and navigation actions MUST be reachable by keyboard in reading order
- **AND** each action MUST show a visible focus indicator
- **AND** the state MUST be announced to assistive technology

#### Scenario: Status surface renders on a narrow viewport

- **WHEN** the status surface renders at a mobile width
- **THEN** its heading and copy MUST wrap within the surface without horizontal page overflow
- **AND** the action controls MUST stack or wrap without overlapping or clipping
