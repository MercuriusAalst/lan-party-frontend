# privacy-safe-participant-data Specification

## Purpose
Defines the front-end public participant data boundary for game, team, match, bracket, and placement displays so public participant surfaces use privacy-safe response data without enriching from private account flows.

## Requirements

### Requirement: Public participant data excludes private account fields
The front-end SHALL model public game and team participant response data with DTOs that exclude private account/internal fields such as email, email verification state, Auth0 ID, roles, deletion state, and timestamps. Public participant user identity MUST carry only a navigation ID and a username, or a display label equal to that username, and MUST NOT carry first name, last name, Discord ID, Steam ID, or Riot ID.

#### Scenario: Anonymous game response contains individual participants
- **WHEN** the front-end deserializes an anonymous public game detail response with individual participants
- **THEN** each participant is represented without email, email verification state, Auth0 ID, roles, deletion state, or timestamps
- **AND** the participant provides a navigation ID and a username, or a display label equal to the username
- **AND** the participant provides no first name, last name, Discord ID, Steam ID, or Riot ID

#### Scenario: Anonymous team response contains members
- **WHEN** the front-end deserializes an anonymous public team or team participant response with members
- **THEN** each member is represented without email, email verification state, Auth0 ID, roles, deletion state, or timestamps
- **AND** member usernames and captain identity are represented when returned by the public API
- **AND** the team and captain technical identifiers required for navigation remain available
- **AND** no member first name, last name, Discord ID, Steam ID, or Riot ID is represented

### Requirement: Public participant data renders usernames only
The front-end MUST render public participant data by username only. It MUST NOT render first name, last name, Discord ID, Steam ID, or Riot ID from public participant DTOs, and detailed opponent identity MUST come from the match-authorized opponent-profile response.

#### Scenario: Public participant fields are present in a response
- **WHEN** a public game, team, tournament, bracket, or placement response contains a user's name or linked platform IDs
- **THEN** the front-end renders only the username
- **AND** the front-end does not issue enrichment requests for those details

#### Scenario: Match-authorized opponent details are returned
- **WHEN** the opponent-profile endpoint authorizes a matched individual or team captain
- **THEN** the front-end may display the username, first name, last name, Discord ID, Steam ID, and Riot ID returned by that endpoint in the match context
- **AND** the front-end does not reuse those details in public participant surfaces

### Requirement: Authorized participant data remains separate from public participant data
The front-end SHALL keep authorized admin/current-user participant models and service flows separate from privacy-safe public participant models.

#### Scenario: Admin adds participants to a game
- **WHEN** an authorized admin opens an add-participant workflow
- **THEN** the workflow may use authorized user or team endpoints that return full details needed for administration
- **AND** those full details are not reused as the source model for anonymous public participant displays

#### Scenario: Current user manages private account data
- **WHEN** a signed-in user views or edits their own profile data
- **THEN** the front-end may use current-user DTOs with private fields
- **AND** those DTOs are not serialized into public game, bracket, placement, or team participant rendering state

### Requirement: Public participant lookup uses loaded response data efficiently
The front-end SHALL resolve public participant display labels and links from the already loaded game or team response data without additional per-participant API calls.

#### Scenario: Bracket renders participant names
- **WHEN** a public bracket renders match participant rows
- **THEN** participant labels are resolved from loaded game participant collections or match payload data
- **AND** no user, team, admin, current-user, or public profile endpoint is called once per participant to render the bracket

#### Scenario: Placements render participant names
- **WHEN** public placements or results render completed tournament rankings
- **THEN** participant labels are resolved from the loaded placement or game participant data
- **AND** missing optional fields are omitted cleanly without triggering enrichment calls
