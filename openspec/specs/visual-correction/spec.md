# visual-correction Specification

## Purpose
TBD - created by archiving change expand-product-redesign. Update Purpose after archive.
## Requirements
### Requirement: Home hero is a full-viewport, readable event entry point

The Home route MUST present the existing dynamic tournament imagery or its
existing fallback as a full-viewport event hero with the current headline,
date/location context, and both current call-to-action links available in the
initial desktop and mobile viewport.

#### Scenario: Desktop visitor opens Home

- **WHEN** a visitor opens Home at a 1440x900 or 1280x900 viewport
- **THEN** the hero MUST occupy the initial viewport using a header-aware
  `min-height: 100dvh` or equivalent minimum
- **AND** the headline MUST fit the intended two-line composition, the image
  MUST share the first hero grid row with the content, and date/location plus
  both existing CTAs MUST be visible without scrolling
- **AND** the page MUST not create horizontal overflow or crop the hero with a
  max-height constraint

#### Scenario: Mobile visitor opens Home

- **WHEN** a visitor opens Home at a 390x844 viewport
- **THEN** the hero MUST reflow into a readable single-column composition
- **AND** the existing image resolver/fallback, date/location, headline, and
  both CTA destinations MUST remain available in the initial hero experience
- **AND** object positioning and scrim treatment MUST preserve essential image
  content and readable copy

#### Scenario: Visitor prefers reduced motion

- **WHEN** the browser reports `prefers-reduced-motion: reduce`
- **THEN** the hero MUST not use animated scroll cues, glow loops, parallax, or
  image drift
- **AND** all hero content and actions MUST remain available

### Requirement: Product surfaces use complete light and dark semantic states

The visual system MUST style custom dialogs, MudBlazor controls and steppers,
filters, tabs, brackets, match dialogs, public relationship cards, team invite
and danger surfaces, nested labels/values, loading/empty/error/status states,
and sponsor/winner/loser/priority states from shared light and dark semantic
tokens. Component-local theme declarations MUST NOT override those tokens in
the rendered state.

#### Scenario: Visitor reaches a corrected interactive surface

- **WHEN** a visitor or authenticated user opens a dialog, filter, tab,
  bracket, match view, profile relationship card, team invite, or form state
- **THEN** the surface MUST render the active theme's neutral canvas/surface
  hierarchy with readable labels, values, placeholders, controls, focus
  indicators, close actions, and status feedback at WCAG AA contrast
- **AND** pending, success, error, blocked, warning, danger, winner, loser,
  podium, sponsor-tier, and priority/type meanings MUST remain visually distinct
- **AND** dark mode MUST use neutral surfaces and off-white text without a
  green tint in general copy or surfaces

#### Scenario: MudBlazor surface is rendered

- **WHEN** a MudBlazor input, select, filter, tab, stepper, dialog, or status
  control is rendered in the product shell
- **THEN** its computed background, text, border, focus, and disabled states
  MUST use the active explicit light or dark theme and accessible
  accent/semantic values
- **AND** no default light, green-tinted dark, or purple theme value MAY be
  visible in the wrong state

### Requirement: Home keeps a useful five-section landing flow

The Home route MUST contain only Hero, Sponsors, Tournaments, Tickets, and
Contact sections in that order. Existing compatibility anchors MAY remain
embedded in the relevant section, but the page MUST not add filler sections or
a redundant anchor rail when the content already fits the flow.

#### Scenario: Visitor opens Home

- **WHEN** a visitor opens Home
- **THEN** the five useful sections MUST be discoverable in the required order
- **AND** redundant title/description blocks and section jump controls MUST be
  omitted when they add no information

### Requirement: Marketing copy and composition stay in preserve mode

Home and marketing surfaces MUST preserve existing fonts, brand palette, hero
content/assets, section order, anchors, and established component details.
Previously identified low-value taglines, descriptive filler, and read-only
explanatory prose MAY be removed only when they do not change a decision, action,
or state. After the hero, Home MAY vary composition across Sponsors,
Tournaments, Tickets, and Contact rather than repeating title-content
enumeration. The Tournaments overview remains governed by its general browse
layout and MUST NOT be structurally rewritten.

#### Scenario: Visitor moves through the Home flow

- **WHEN** a visitor moves through Home after the hero
- **THEN** Sponsors, Tournaments, Tickets, and Contact MUST retain their
  existing content purpose and required actions or anchors
- **AND** adjacent sections MAY use different composition families when that
  improves hierarchy without adding filler
- **AND** low-value taglines and read-only explanatory prose MUST not be
  rendered when they do not change a decision, action, or state

#### Scenario: Visitor reaches Tickets and Contact

- **WHEN** Tickets and Contact are rendered at a desktop width
- **THEN** they MUST share the same responsive final row while retaining their
  existing actions and anchors
- **AND** they MUST stack in reading order at narrow widths without page-wide
  horizontal overflow

### Requirement: Motion and contrast use the existing foundation

Motion and contrast corrections MUST use the existing CSS, JavaScript, Blazor,
and MudBlazor stack. No new package or dependency is permitted for this pass.
Purposeful motion MAY be added only through that stack. Each
animation MUST communicate hierarchy, feedback, storytelling, or a state
transition and MUST have a static or `prefers-reduced-motion` equivalent. Dark
contrast corrections MUST be made at the owning selector or semantic token and
MUST meet WCAG AA without broad global recoloring, brand-palette replacement,
or broad `!important` overrides.

#### Scenario: Visitor prefers reduced motion

- **WHEN** the browser reports `prefers-reduced-motion: reduce`
- **THEN** purposeful motion MUST settle to a static or immediate presentation
- **AND** the same content, actions, statuses, and focus behavior MUST remain

#### Scenario: Dark mode is rendered

- **WHEN** text, controls, focus states, or status surfaces render in dark mode
- **THEN** their foreground/background pairings MUST meet WCAG AA
- **AND** the correction MUST preserve the existing brand palette and local
  component distinctions

### Requirement: Visual hierarchy uses spacing, restrained borders, and purposeful motion

Shared and page-local composition MUST use spacing and typography before borders,
avoid duplicate parent/child frames, and provide enough separation between
labels, controls, and content. Motion MAY support hierarchy, feedback, or state
transitions, but it MUST not be the only state cue and MUST honor reduced-motion
preferences.

#### Scenario: Priority or type content is rendered

- **WHEN** a priority/type item such as a "what to bring" item is rendered
- **THEN** its distinct accent or status MUST remain visible in both themes
- **AND** an icon, label, or accessible text MUST supplement color when color
  alone would not convey the distinction

### Requirement: Browser-computed-style evidence gates completion

The corrective implementation MUST include browser-computed-style evidence
before its tasks are marked complete.

#### Scenario: Representative mock routes are checked

- **WHEN** browser QA exercises representative public, authenticated, and
  organizer routes in mock mode at 1440x900, 1280x900, and 390x844 widths in
  both light and dark modes
- **THEN** it MUST inspect computed foreground/background/border/focus styles,
  hero bounds, document width, reachable dialog states, and priority/type
  distinctions
- **AND** it MUST record contrast and overflow results for the corrected
  surfaces before completion

#### Scenario: A live or Auth0 path is unavailable

- **WHEN** live backend or Auth0 access cannot be exercised in the environment
- **THEN** the implementation MUST document that limitation
- **AND** it MUST still validate static contracts, mock parity, public routes,
  and every reachable local state without weakening the API/auth boundaries

### Requirement: Dark private and legal surfaces use readable semantic states

Authenticated private-profile surfaces and the Privacy page MUST use the
active dark semantic canvas, surface, and text tokens. Private email MUST be
readable in dark mode, and Privacy content MUST not fall back to bright white
cards. Existing privacy boundaries, legal/privacy copy, hierarchy, and field
ownership MUST remain unchanged.

#### Scenario: Authenticated profile displays private email

- **WHEN** an authenticated user views the existing private profile form in
  dark mode
- **THEN** the email value, label, helper, validation, and surrounding surface
  MUST meet WCAG AA contrast
- **AND** the field MUST remain private and retain its existing name, order,
  edit behavior, and submission callback

#### Scenario: Visitor opens the Privacy page in dark mode

- **WHEN** the Privacy page renders with the dark theme selected
- **THEN** its cards and content surfaces MUST use dark semantic values rather
  than bright white backgrounds
- **AND** legal/privacy copy, headings, links, hierarchy, and reading order MUST
  remain intact and readable

### Requirement: Dark input interaction states remain dark and readable

Inputs and selectable controls MUST keep a dark semantic surface and readable
foreground in dark-mode focus, selected, checked, and autofill states. Focus
indicators MUST remain visible without turning the control into a bright card or
green-tinted general surface. Existing field names, order, labels, validation,
autofill semantics, and submission behavior MUST remain unchanged.

#### Scenario: User focuses or selects a dark-mode input

- **WHEN** a user focuses, selects, checks, or autofills a form control in dark
  mode
- **THEN** the control background, value, placeholder, label, and focus/selected
  indicator MUST remain readable at WCAG AA contrast
- **AND** the state MUST not change the form's field order, value semantics, or
  existing callback behavior

### Requirement: Info packing priorities retain contrast and meaning

The Info packing guide MUST preserve distinct hardware, basic, and optional
priority/type accents in light and dark themes. Dark-mode item names, group
labels, icons, accent edges, and tinted surfaces MUST meet WCAG AA where text is
present, while the detailed packing content and existing anchors remain intact.

#### Scenario: Visitor views the packing guide in dark mode

- **WHEN** the Info packing guide renders with dark mode selected
- **THEN** hardware, basic, and optional items MUST remain visibly distinct
  through their existing semantic accent, icon, or label treatment
- **AND** item names, group labels, and icons MUST remain readable without
  broadly recoloring the site or changing the light-theme palette

### Requirement: Home supporting layouts use proportionate widths

The Home page MUST give its featured tournament and final action areas a
proportionate layout that follows the surrounding page rhythm rather than
making content feel unnecessarily boxed or vertically off-center.

#### Scenario: Visitor views featured tournaments

- **WHEN** the Home featured tournament section renders at a desktop or tablet
  width
- **THEN** the lead presentation MUST use a natural narrower measure relative
  to the page and nearby sections
- **AND** the content MUST retain its adjacent tournament list, image,
  identity, status, and detail action without an oversized enclosing frame

#### Scenario: Visitor views Join the LAN and Contact

- **WHEN** the final Home action row renders at a desktop or tablet width
- **THEN** the Contact area MUST receive more horizontal space than Join the LAN
- **AND** the two areas MUST remain visually balanced and aligned

#### Scenario: Visitor views the final Home row on a narrow screen

- **WHEN** the viewport reaches a supported narrow breakpoint
- **THEN** Join the LAN and Contact MUST stack in reading order
- **AND** neither section MUST appear vertically off-center or create page-wide
  horizontal scrolling

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
