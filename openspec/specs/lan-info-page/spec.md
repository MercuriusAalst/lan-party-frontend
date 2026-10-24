## Purpose

Define the public LAN information page that helps visitors prepare for the event, find the venue, compare ticket options, review catering, and contact the organizers.
## Requirements
### Requirement: Info page is available as a public visitor destination

The site SHALL provide a public LAN information page that can be reached without authentication and is discoverable from the site's primary visitor navigation.

#### Scenario: Visitor opens the info page
- **WHEN** a visitor navigates to the LAN information route
- **THEN** the page renders without requiring sign-in
- **AND** the page title identifies it as LAN party information
- **AND** the visible content is organized into preparation, when and where, tickets, catering, and contact sections

#### Scenario: Visitor uses site navigation
- **WHEN** a visitor uses the primary site navigation to find practical event details
- **THEN** the navigation exposes a route or link to the LAN information page
- **AND** selecting that navigation item takes the visitor to the information page

### Requirement: Info page presents what participants need to bring as a visual packing guide

The LAN information page SHALL show what participants need to bring using an illustrated, image-led, or otherwise visual packing guide instead of a simple text-only list.

#### Scenario: Visitor reviews the packing guide
- **WHEN** a visitor views the preparation section
- **THEN** the page presents computer, max 2 monitors, power cables, power strip, headset, mouse, keyboard, mousepad, phone and charger, and ID in a visual packing-guide treatment
- **AND** the presentation is richer than a simple text list

#### Scenario: Visitor distinguishes item groups
- **WHEN** a visitor views the preparation section
- **THEN** gaming essentials are visually distinguishable from basic personal items
- **AND** the visual treatment includes accessible text or labels for the represented items

### Requirement: Info page presents event timing and location with a map

The LAN information page SHALL show when the event takes place and where it is located, including a map for Kwalestraat 154, 9320 Aalst at Odisee Aalst.

#### Scenario: Visitor checks the event location
- **WHEN** a visitor views the when and where section
- **THEN** the page displays Kwalestraat 154, 9320 Aalst as the event address
- **AND** the page identifies the venue as Odisee Aalst
- **AND** the page displays an embedded or otherwise visible map for the address

#### Scenario: Visitor needs directions
- **WHEN** a visitor views the location map
- **THEN** the page provides a link or control that opens the location in an external maps experience
- **AND** the map area remains usable on narrow and wide viewports without causing horizontal scrolling

#### Scenario: Visitor checks event timing
- **WHEN** a visitor views the when and where section
- **THEN** the page displays the centrally configured event date and time or event window
- **AND** the timing information is presented near the location information
- **AND** the timing information is not derived from tournament or game data

### Requirement: Info page presents ticket pricing as vintage tickets

The LAN information page SHALL present the available ticket types and their prices using a visual treatment inspired by vintage physical tickets.

#### Scenario: Visitor compares ticket options
- **WHEN** a visitor views the tickets section
- **THEN** the page displays Gamer + gaming chair as EUR 15 first
- **AND** the page displays Gamer as EUR 10
- **AND** the page displays Visitors as free with no competition participation last

#### Scenario: Visitor scans ticket differences
- **WHEN** multiple ticket types are shown
- **THEN** each option includes a label and price inside a ticket-shaped or ticket-inspired visual unit
- **AND** the visitor can distinguish spectator access from gamer competition participation

#### Scenario: Visitor recognizes the ticket treatment
- **WHEN** the ticket pricing section is visible
- **THEN** the ticket options use visual details such as perforation, stub, punched, or printed-ticket styling
- **AND** the ticket styling remains readable on narrow and wide viewports
- **AND** ticket prices are not rendered vertically
- **AND** ticket numbers are not displayed

### Requirement: Info page presents catering as a restaurant-style menu

The LAN information page SHALL present catering options and current prices in a restaurant food-menu style without making specific menu items part of the permanent spec contract.

#### Scenario: Visitor reviews current drinks
- **WHEN** a visitor views the catering section
- **THEN** the page shows the currently configured drinks with visible prices
- **AND** the drinks are presented as part of a restaurant-style menu rather than a plain data list

#### Scenario: Visitor reviews current snacks
- **WHEN** a visitor views the catering section
- **THEN** the page shows the currently configured snacks with visible prices
- **AND** the snacks are presented as part of a restaurant-style menu rather than a plain data list

#### Scenario: Visitor compares catering categories
- **WHEN** drinks and snacks are both available on the page
- **THEN** the page groups drinks separately from snacks
- **AND** every listed menu item includes a visible price
- **AND** Euro prices place `EUR` after the numeric value

### Requirement: Info page provides contact and social links

The LAN information page SHALL provide a contact form and links to the Mercurius Aalst Discord, Facebook, and Instagram destinations.

#### Scenario: Visitor contacts the organizers
- **WHEN** a visitor views the contact section
- **THEN** the page displays a contact form with fields for the visitor's name, contact address, and message
- **AND** the form can be used without requiring authentication

#### Scenario: Visitor submits the contact form
- **WHEN** a visitor submits valid contact form details
- **THEN** the system sends an email containing the visitor's name, contact address, and message to the configured organizer recipient
- **AND** the browser does not open the visitor's local mail application

#### Scenario: Visitor opens social destinations
- **WHEN** a visitor views the contact section
- **THEN** the page links to the Discord server at `https://discord.gg/WrpDDbNsje`
- **AND** the page links to the Facebook page at `https://www.facebook.com/MercuriusAalst`
- **AND** the page links to the Instagram profile for `mercurius.aalst`

### Requirement: LAN event data is centrally managed

The LAN information page and other event-facing surfaces SHALL use centralized configuration for reusable LAN event details.

#### Scenario: Reusable event details are needed
- **WHEN** the site renders event date, address, map, social links, ticket options, packing items, or catering entries
- **THEN** those values are read from centralized application configuration
- **AND** equivalent values are not duplicated as independent hardcoded constants across pages

### Requirement: Info page uses the existing visual style with supportive motion

The LAN information page SHALL match the established Mercurius LAN visual language and use icons or motion only where they improve scanning or orientation.

#### Scenario: Visitor scans dense information
- **WHEN** a visitor views packing guide, ticket, catering, location, or contact sections
- **THEN** the page uses visual grouping, icons, or compact section treatments to make each information type easy to identify
- **AND** the page reuses existing brand styling patterns such as section eyebrows, brand sections, shared buttons, cards, or scoped page styling

#### Scenario: Visitor prefers reduced motion
- **WHEN** the visitor's browser requests reduced motion
- **THEN** nonessential animations on the information page are disabled or reduced
- **AND** all information remains visible and understandable

#### Scenario: Visitor uses a small screen
- **WHEN** the information page is rendered on a narrow viewport
- **THEN** the sections stack into a readable mobile layout
- **AND** text, controls, map content, and price lists remain inside their containers without overlapping or horizontal scrolling

### Requirement: Info ticket options use intentional visual hierarchy

The Info page MUST distinguish ticket options with a clear visual hierarchy,
using the main theme green for ticket names while preserving neutral ticket
surfaces, readable text, existing ticket content, and the existing
ticket-inspired presentation.

#### Scenario: Visitor compares tickets in either theme

- **WHEN** a visitor views the ticket section in light or dark mode
- **THEN** each ticket option MUST have a clear visual hierarchy that supports
  comparing the ticket name, description, and price
- **AND** the text and price MUST remain readable at accessible contrast
- **AND** labels or structure MUST still identify the options if color is not
  perceived

#### Scenario: Visitor views tickets on a narrow viewport

- **WHEN** the ticket section is rendered at a supported mobile or desktop
  breakpoint
- **THEN** the ticket options MUST remain inside their owning container
- **AND** the hierarchy MUST remain scannable without overlap, clipped prices,
  or page-wide horizontal scrolling

### Requirement: Info page identifies the event and explains ticket purchases

The Info page SHALL use a localized page heading that identifies the event's
complete information and SHALL explain the available ticket purchase channels.

#### Scenario: Visitor reviews ticket guidance

- **WHEN** a visitor views the Info page in English or Dutch
- **THEN** the page heading MUST read "All information for the LAN" in English
  and "Alle informatie over de LAN" in Dutch
- **AND** the tickets section MUST state that tickets are available through
  Google Forms or at the venue
- **AND** the Google Forms link MUST open
  `https://forms.gle/B6Jwgbb51KU2Hmds6`
- **AND** the guidance MUST remain readable without horizontal overflow at
  supported desktop and mobile widths

