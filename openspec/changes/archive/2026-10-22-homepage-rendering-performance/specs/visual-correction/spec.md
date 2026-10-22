## ADDED Requirements

### Requirement: Home tournament hero artwork remains sharp

The Home hero MUST render its featured tournament artwork without a blur
filter. The existing image order, size, opacity, rotation, and drift MUST
remain. The surrounding glow, fade, gradients, and card backdrops MUST remain
independent of the artwork blur removal.

#### Scenario: Home visitor sees featured tournament artwork

- **WHEN** Home renders one or more featured tournament images
- **THEN** each image MUST render without a CSS filter blur
- **AND** the existing image motion and atmospheric hero layers MUST remain
- **AND** a missing image MUST continue to omit only that image

### Requirement: Home hero image URLs are available during initial rendering

The Home page MUST make featured image URLs available in its initial
server-prerendered document when featured tournament data is available. It MUST
not wait for the interactive component's post-render data load. The
prerender-to-interactive handoff MUST reuse that featured result without
duplicating the initial featured-data API request. The hero MUST continue to
load without blocking sponsor data, and later refreshes MUST preserve confirmed
featured order and disposal safety.

#### Scenario: Home visitor opens the page with featured tournament data

- **WHEN** the featured tournament data request succeeds during initial
  prerender
- **THEN** the initial rendered document MUST contain the ordered hero image
  URLs so the browser can start fetching them immediately
- **AND** interactive hydration MUST consume the prerendered result without a
  second initial featured-data request
- **AND** sponsor loading and the existing error, retry, admin-save, and
  disposal behavior MUST remain independent
