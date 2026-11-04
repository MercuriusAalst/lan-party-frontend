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

Each ticket option SHALL be a clearly interactive, accessible link to its configured Google Forms registration URL. The form URL and field mappings SHALL be read from application configuration. Signed-in visitors MAY have their current application profile's first name, last name, and username prefilled when activating a ticket link. Anonymous visitors SHALL retain a usable ticket-only registration link. Personal profile values SHALL be fetched only for the current user at activation time and SHALL NOT be placed in rendered markup, shared caches, or logs. The interface SHALL disclose that available account details are sent to Google when opening the form.

#### Scenario: Visitor opens a ticket form
- **WHEN** a visitor activates any part of a configured ticket card by mouse, touch, or keyboard
- **THEN** the matching ticket registration form opens in a new tab with its configured ticket choice selected
- **AND** the visitor can still change the selection in Google Forms
- **AND** the link exposes a localized accessible name and visible keyboard focus

#### Scenario: Signed-in visitor opens a ticket form
- **WHEN** an authenticated visitor activates a ticket card
- **THEN** available first name, last name, and username from that visitor's current application profile are added using the configured form field mappings
- **AND** the values are URL-encoded and remain editable in the form
- **AND** no email address is added

#### Scenario: Profile or optional configuration is unavailable
- **WHEN** the visitor is anonymous, profile loading fails, a profile field is empty, or a field mapping is missing
- **THEN** registration continues using the configured ticket-only URL with available mapped fields omitted
- **AND** missing or invalid optional form configuration does not prevent the Info page from rendering

#### Scenario: Visitor is informed about profile transfer
- **WHEN** the visitor views the ticket cards
- **THEN** localized English and Dutch text explains that opening a form transfers available account details to Google Forms
- **AND** the form is never submitted automatically

#### Scenario: Account changes while profile data loads
- **WHEN** the visitor signs out or another account becomes current in a different tab while profile data is loading
- **THEN** the form opens with the ticket-only URL and no profile values from the previous account
