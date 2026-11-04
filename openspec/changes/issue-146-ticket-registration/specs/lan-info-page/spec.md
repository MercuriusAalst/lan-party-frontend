## MODIFIED Requirements

### Requirement: Info page identifies the event and explains ticket purchases

The Info page SHALL use a localized page heading that identifies the event's complete information and SHALL explain the available ticket purchase channels.

#### Scenario: Visitor reviews ticket guidance
- **WHEN** a visitor views the Info page in English or Dutch
- **THEN** the page heading MUST read "All information for the LAN" in English and "Alle informatie over de LAN" in Dutch
- **AND** the tickets section MUST state that tickets are available through Google Forms or at the venue
- **AND** the general Google Forms link MUST open the configured registration URL
- **AND** the guidance MUST remain readable without horizontal overflow at supported desktop and mobile widths

### Requirement: Info ticket cards open their configured registration form

Each ticket option SHALL be a clearly interactive, accessible link to its configured Google Forms registration URL. The form URL and field mappings SHALL be read from application configuration. When the Info page loads for an authenticated visitor, it MAY fetch that visitor's current application profile and include available first name, last name, and username values in the rendered ticket link. Anonymous visitors and visitors whose profile cannot be loaded SHALL retain usable ticket-only registration links. Profile values are included only in the authenticated page's ticket link and are not cached or logged by the application.

#### Scenario: Visitor opens a ticket form
- **WHEN** a visitor activates any part of a configured ticket card by mouse, touch, or keyboard
- **THEN** the matching ticket registration form opens in a new tab with its configured ticket choice selected
- **AND** the visitor can still change the selection in Google Forms
- **AND** the link exposes a localized accessible name and visible keyboard focus
- **AND** the form is never submitted automatically

#### Scenario: Signed-in visitor loads the Info page
- **WHEN** an authenticated visitor loads the Info page and their current application profile is available
- **THEN** available first name, last name, and username from that profile are added to each ticket link using the configured form field mappings
- **AND** the values are URL-encoded and remain editable in the form
- **AND** no email address is added

#### Scenario: Profile or optional configuration is unavailable
- **WHEN** the visitor is anonymous, profile loading fails, a profile field is empty, or a field mapping is missing
- **THEN** registration continues using the configured ticket-only URL with available mapped fields omitted
- **AND** missing or invalid optional form configuration does not prevent the Info page from rendering

#### Scenario: Visitor signs out and reloads the Info page
- **WHEN** an authenticated visitor signs out and the Info page is rendered again without an authenticated user
- **THEN** ticket links contain only the configured ticket selection and no profile values
