# sponsored-tournament-titles Specification

## Purpose
TBD - created by archiving change issue-93-presented-by-sponsor. Update Purpose after archive.
## Requirements
### Requirement: Prominent public tournament titles show the presenting partner

Prominent public tournament titles MUST show a localized presented-by attribution when the tournament has a sponsor placement with a usable sponsor name. The attribution MUST be omitted when the tournament has no placement or the sponsor name is blank or whitespace. The tournament name MUST remain unchanged.

#### Scenario: Sponsored tournament title on the tournaments overview

- **WHEN** a visitor views the tournaments overview and a sponsored tournament card renders
- **THEN** the card MUST show the localized presented-by attribution built from the placement sponsor name beside or below the tournament title
- **AND** the attribution MUST use the existing `Feature.tournaments.partnerHeading` resource for the active language

#### Scenario: Sponsored tournament title on the home featured cards

- **WHEN** a sponsored tournament renders in a home page featured card
- **THEN** the card MUST show the localized presented-by attribution built from the placement sponsor name
- **AND** both the lead card and the compact row cards MUST show the attribution

#### Scenario: Sponsored tournament hero title on the detail page

- **WHEN** a visitor opens a sponsored tournament detail page
- **THEN** the hero title MUST show the localized presented-by attribution built from the placement sponsor name

#### Scenario: Unsponsored tournament title

- **WHEN** a tournament has no sponsor placement
- **THEN** no presented-by attribution MUST be rendered on any public tournament title

#### Scenario: Sponsor name is blank

- **WHEN** a tournament placement exists but its sponsor name is empty or whitespace
- **THEN** no presented-by attribution MUST be rendered
- **AND** the tournament title MUST still render normally

#### Scenario: Long sponsor name wraps

- **WHEN** the presented-by attribution contains a long sponsor name
- **THEN** the attribution MUST wrap inside its container at mobile and desktop widths without overflowing the card or hero

### Requirement: Public tournament projections carry the sponsor placement

The public tournament list projection and the tournament detail projection MUST deserialize the existing singular `sponsorPlacement` field returned by the API. No new endpoint, request field, or response field is introduced.

#### Scenario: Public tournament list carries a placement

- **WHEN** the front end reads the public tournament list
- **THEN** each tournament MUST expose its sponsor placement, including the sponsor name
- **AND** the list MUST NOT require a second request per tournament to obtain the placement

#### Scenario: Tournament without a placement

- **WHEN** the API returns a tournament list or detail entry without a sponsor placement
- **THEN** the front end MUST treat the placement as absent and render no attribution

