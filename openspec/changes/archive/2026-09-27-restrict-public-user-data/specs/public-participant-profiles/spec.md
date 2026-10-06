## MODIFIED Requirements

### Requirement: Public user profile exposes privacy-scoped fields
The system SHALL provide `/users/{username:string}` as an admin-only user-detail page. The page
MUST load the full record through the admin user resource. Other public surfaces MUST display a
user by username only.

#### Scenario: Anonymous or non-admin visitor opens a user-detail page
- **WHEN** an anonymous visitor or authenticated non-admin opens `/users/{username}`
- **THEN** the route MUST require the admin role
- **AND** it MUST NOT request or render user detail data

#### Scenario: Admin opens a user-detail page
- **WHEN** an admin opens `/users/{username}` for a known user
- **THEN** the page MUST request `GET /v1/lan/users/{username}`
- **AND** it MAY render the returned full user record

#### Scenario: Public surface renders a user identity
- **WHEN** a public-facing surface renders a user outside an authorized match context
- **THEN** it MUST display only the username
- **AND** it MUST NOT render a user-detail link or expose names, linked IDs, email, roles, or other account fields

### Requirement: Team member click navigates to public user profile
Public team rosters MUST render member usernames as non-navigable text; team profile navigation
MUST remain available for team identities.

#### Scenario: Visitor views a public team roster
- **WHEN** a public team profile displays a member
- **THEN** it MUST display the member's username without linking to `/users/{username}`
- **AND** it MAY identify the captain inline

### Requirement: Participant surfaces link only returned public identifiers
Participant surfaces MUST display public user identities by username only and MUST NOT link user
identities to `/users/{username}`. Team identity links MAY continue to use a returned team name.

#### Scenario: Public participant has a username
- **WHEN** a public participant surface renders an individual or team member with a username
- **THEN** it MUST display that username without a user-profile link
- **AND** it MUST NOT display names or linked platform IDs

### Requirement: Public profile services use the current public resource contracts
The public user resource MUST be used only for its username-only response. The user-detail page
MUST use the admin user resource after the frontend admin-role gate. User match summaries on that
page MUST use the admin-only match-summary resource.

#### Scenario: Anonymous public user lookup
- **WHEN** a public user lookup is requested
- **THEN** the frontend contract MUST accept only the returned username

#### Scenario: Admin user-detail lookup
- **WHEN** an admin opens a user-detail page
- **THEN** the frontend MUST request the admin user resource at `/v1/lan/users/{username}`

#### Scenario: Admin opens user match summaries
- **WHEN** an admin opens a user-detail page
- **THEN** the frontend MUST request match summaries from `/v1/lan/users/{username}/match-summaries`

### Requirement: Contextual user identities open privacy-safe information popups
Detailed user information MUST be requested through the match-authorized opponent-profile resource
only from tournament match context. Tournament participant-list identities MUST NOT open user
detail popups.

#### Scenario: Participant selects an opponent in a match
- **WHEN** an authenticated participant requests their opponent's details from a match view
- **THEN** the frontend MUST request `GET /v1/lan/matches/{id}/opponent-profile`
- **AND** it MUST render only the authorized username, first name, last name, Discord ID, Steam ID,
  and Riot ID returned by that resource
- **AND** it MUST handle forbidden or missing opponent details without falling back to public or
  admin user resources

#### Scenario: Visitor selects a participant-list identity
- **WHEN** a visitor selects a team or user in a tournament participant list
- **THEN** the list MUST NOT open a specific user-detail popup
- **AND** user identities MUST remain username-only
