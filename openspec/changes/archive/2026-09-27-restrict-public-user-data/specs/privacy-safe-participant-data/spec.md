## MODIFIED Requirements

### Requirement: Public participant data renders usernames only
Public participant data MAY expose usernames for identification. The frontend MUST NOT render
first name, last name, Discord ID, Steam ID, or Riot ID from public participant DTOs. Detailed
opponent identity MUST come from the match-authorized opponent-profile response.

#### Scenario: Public participant fields are present in a response
- **WHEN** a public game, team, tournament, bracket, or placement response contains a user's name
  or linked platform IDs
- **THEN** the frontend MUST render only the username
- **AND** it MUST NOT issue enrichment requests for those details

#### Scenario: Match-authorized opponent details are returned
- **WHEN** the opponent-profile endpoint authorizes a matched individual or team captain
- **THEN** the frontend MAY display the username, first name, last name, Discord ID, Steam ID, and
  Riot ID returned by that endpoint in the match context
- **AND** it MUST NOT reuse those details in public participant surfaces
