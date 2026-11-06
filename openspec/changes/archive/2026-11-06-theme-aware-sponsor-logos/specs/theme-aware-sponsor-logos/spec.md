## ADDED Requirements

### Requirement: Administrators can manage a contrasting light logo

Sponsor creation MUST require a default logo and MAY include a light-coloured
logo intended for dark backgrounds. Sponsor editing MUST allow the default and
light logo to be retained, replaced, or explicitly removed independently.
Submitting an unrelated sponsor edit without a new light-logo file MUST retain
the existing light logo.

#### Scenario: Administrator creates a sponsor with two logos

- **WHEN** an administrator supplies a required default logo and an optional
  light-coloured logo
- **THEN** the create request MUST include both files using the backend multipart
  contract
- **AND** the saved sponsor MUST expose both resolved logo URLs

#### Scenario: Administrator edits and retains a light logo

- **WHEN** an administrator changes unrelated sponsor fields without selecting
  a new light-logo file and without requesting removal
- **THEN** the existing light logo MUST remain associated with the sponsor

#### Scenario: Administrator explicitly removes a light logo

- **WHEN** an administrator chooses to remove the light logo
- **THEN** the update request MUST explicitly request that removal
- **AND** the saved sponsor MUST no longer expose a light logo URL

#### Scenario: Upload fails

- **WHEN** a sponsor logo upload fails validation or transport
- **THEN** the form MUST show a localized recoverable error
- **AND** the administrator MUST be able to correct or retry the upload without
  losing the other sponsor form values

### Requirement: Sponsor logo controls explain theme usage and preview both surfaces

Sponsor management MUST localize the default and light-logo labels, purpose,
selection, replacement, and removal controls in English and Dutch. It MUST show
a preview of the resolved artwork on representative light and dark surfaces.

#### Scenario: Administrator reviews logo behavior

- **WHEN** the sponsor form renders
- **THEN** it MUST explain that the default logo is used on light backgrounds
  and the optional light-coloured logo is used on dark backgrounds
- **AND** it MUST explain that a single supplied logo is used in both themes
- **AND** both preview surfaces MUST use the same resolution and fallback rules
  as public sponsor rendering

### Requirement: Sponsor logos follow the active application theme

All sponsor logo placements MUST use the shared theme-aware resolution behavior.
In light mode they MUST display the default logo. In dark mode they MUST display
the optional light logo when available and otherwise display the default logo.
Changes between light and dark mode MUST update the displayed artwork without a
page refresh and MUST follow the persisted application theme when it differs
from the operating-system preference.

#### Scenario: Sponsor has two logos

- **WHEN** the active application theme changes between light and dark mode
- **THEN** the contrasting sponsor artwork MUST change immediately to match the
  active theme

#### Scenario: Sponsor has one logo

- **WHEN** a sponsor has no light logo URL
- **THEN** its default logo MUST render in both light and dark mode

#### Scenario: API response omits the optional logo field

- **WHEN** a legacy sponsor or tournament sponsor response omits
  `lightLogoUrl`
- **THEN** the frontend MUST treat the light logo as absent
- **AND** it MUST render the default logo in both themes

### Requirement: Sponsor logo placement preserves presentation and accessibility

Every sponsor logo location MUST use the same theme-aware behavior. This includes
the homepage sponsor showcase, public sponsors page, sponsor search, and
tournament sponsor placement while preserving its sponsor destination,
responsive sizing, layout, keyboard access, and sponsor-identifying alternative
text.

#### Scenario: Theme-aware logo appears in a sponsor placement

- **WHEN** a sponsor placement renders in either active theme
- **THEN** the displayed logo MUST follow the shared resolution and fallback
  behavior
- **AND** the existing sponsor link and accessible name MUST remain available
- **AND** the logo MUST remain within its responsive container without clipping
  or causing page-wide overflow

#### Scenario: Tournament sponsor projection carries both logo URLs

- **WHEN** tournament sponsor placement data includes a light logo URL
- **THEN** the public tournament placement MUST resolve and display that URL in
  dark mode
- **AND** it MUST fall back to the placement's default logo in light mode or
  when the optional URL is absent
