## ADDED Requirements

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
