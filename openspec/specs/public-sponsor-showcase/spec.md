# public-sponsor-showcase Specification

## Purpose
TBD - created by archiving change improve-public-sponsors-page. Update Purpose after archive.
## Requirements
### Requirement: Public sponsors page introduces partners with clear actions
The public sponsors page SHALL present a polished partner-focused intro that uses the site's existing brand styling primitives and provides clear visitor actions for sponsor discovery and sponsor inquiries.

#### Scenario: Visitor opens sponsors page with sponsors available
- **WHEN** a visitor opens the public sponsors page and sponsor data is available
- **THEN** the page presents a sponsor-focused intro with stronger visual hierarchy than a plain heading and paragraph
- **AND** the intro includes a call to action for browsing current partners
- **AND** the intro includes a call to action for becoming a sponsor
- **AND** the intro remains consistent with the existing redesign system and shared brand/button classes

#### Scenario: Visitor follows sponsor inquiry action
- **WHEN** the visitor uses the become-a-sponsor call to action from populated or empty sponsor content
- **THEN** the action MUST navigate to `/info#contact-info`
- **AND** the existing contact form MUST be in the browser viewport after navigation
- **AND** the page does not require a new backend form, API endpoint, or sponsor data field

### Requirement: Public sponsors page explains sponsor value
The public sponsors page SHALL include a concise section that explains why sponsors and partners matter to the LAN event experience.

#### Scenario: Sponsor value section is displayed
- **WHEN** the public sponsors page renders
- **THEN** the page shows short visitor-facing content explaining how sponsors support the event experience
- **AND** the content fits the existing brand/card visual system
- **AND** the content does not depend on new backend-provided copy

### Requirement: Public sponsors page preserves sponsor data source and tier model
The public sponsors page SHALL continue to use existing sponsor data from `SponsorService` and the existing `SponsorTier` hierarchy.

#### Scenario: Sponsors are loaded
- **WHEN** sponsor data is loaded for the public sponsors page
- **THEN** sponsors are grouped by their existing `SponsorTier`
- **AND** tier ordering follows the existing display order of Presenting, Gold, Silver, and Bronze
- **AND** sponsor rendering uses the existing sponsor fields for name, tier, logo URL, info URL, and optional description
- **AND** no backend, API, persistence, or sponsor model change is required

#### Scenario: A tier has no sponsors
- **WHEN** sponsors are available but a specific sponsor tier has no sponsors
- **THEN** the page omits that empty tier section
- **AND** the remaining tiers keep their expected visual order

### Requirement: Sponsor tiers have distinct visual hierarchy
The public sponsors page SHALL present sponsor tiers as visually distinct sections that communicate tier hierarchy, with Presenting sponsors receiving the strongest emphasis.

#### Scenario: Presenting sponsors are available
- **WHEN** the page renders one or more Presenting sponsors
- **THEN** Presenting sponsors are shown in the most prominent tier treatment
- **AND** their cards use larger logo space and stronger card emphasis than lower tiers
- **AND** the tier section clearly identifies them as Presenting partners

#### Scenario: Lower sponsor tiers are available
- **WHEN** the page renders Gold, Silver, or Bronze sponsors
- **THEN** each tier uses a visually distinct section or grid treatment
- **AND** lower tiers remain polished and scannable without competing with the Presenting tier emphasis
- **AND** the tier grid responds cleanly from desktop to mobile widths

### Requirement: Sponsor cards are polished, accessible, and responsive
The public sponsors page SHALL render sponsor cards with improved presentation while preserving sponsor link behavior and accessibility.

#### Scenario: Sponsor card renders
- **WHEN** a sponsor is displayed in a tier grid
- **THEN** the card shows the sponsor logo, sponsor name, tier label, and an external-link affordance
- **AND** the card links to the sponsor's configured information URL
- **AND** the link uses safe external link attributes
- **AND** the logo includes accessible alt text identifying the sponsor

#### Scenario: Sponsor description exists
- **WHEN** a sponsor has a non-empty description
- **THEN** the card may display the description according to the tier's visual density
- **AND** the layout remains balanced if descriptions are missing or vary in length

### Requirement: Sponsors page has a useful empty state
The public sponsors page SHALL show a polished empty state when no sponsors are available.

#### Scenario: No sponsors are available
- **WHEN** the sponsor service returns no sponsors
- **THEN** the page shows a branded empty state that explains partner opportunities are available or being finalized
- **AND** the empty state includes a clear become-a-sponsor call to action
- **AND** the empty state does not render empty sponsor tier grids

### Requirement: Sponsors page prioritizes current sponsor content

The public Sponsors page MUST give available current sponsors stronger visual
and viewport priority than its introductory or explanatory copy while keeping a
coherent visitor-facing page hierarchy at every supported breakpoint.

#### Scenario: Visitor opens the Sponsors page with sponsors available

- **WHEN** sponsor data is available
- **THEN** the current sponsor content MUST appear earlier in the page flow and
  MUST be the primary visual attraction
- **AND** the top introduction MUST remain useful while using less vertical
  weight than a large hero treatment
- **AND** existing SponsorService data, tier ordering, sponsor links, and
  accessible labels MUST remain unchanged

#### Scenario: Visitor reads why sponsors matter

- **WHEN** the `Why sponsors matter` section is rendered
- **THEN** the redundant extra description paragraph MUST be omitted
- **AND** the section MUST retain useful value content that explains the role
  of sponsors without visual noise
- **AND** the existing become-a-sponsor action MUST be available in that
  section at a logical point in the reading order

#### Scenario: Visitor reaches a sparse or empty sponsor state

- **WHEN** sponsor data is empty, loading, or unavailable
- **THEN** the page MUST retain its existing loading, empty, or error meaning
- **AND** the available sponsorship action MUST remain discoverable
- **AND** the responsive layout MUST not create page-wide overflow

