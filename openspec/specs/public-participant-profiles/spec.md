# public-participant-profiles Specification

## Purpose
TBD - created by archiving change add-global-menu-search-public-profiles. Update Purpose after archive.
## Requirements
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

#### Scenario: Missing linked identities are omitted cleanly

- **WHEN** a visitor opens `/users/{username}` for a known complete user whose
  public API response omits one or more linked IDs
- **THEN** the page MUST not render rows, cards, placeholders, or labels for the
  missing linked IDs
- **AND** the remaining profile content MUST stay visible and well-formed

#### Scenario: Public surface renders a user identity

- **WHEN** a public-facing surface renders a user outside an authorized match context
- **THEN** it MUST display only the username
- **AND** it MUST not render a user-detail link or expose names, linked IDs, email, roles, or other account fields

#### Scenario: User profile uses focused branded layout

- **WHEN** a visitor opens `/users/{username}` for a known user
- **THEN** the page MUST present the profile as a branded participant profile
  surface with a clear identity header and available next public context
- **AND** the page MUST keep username visible as the route and navigation
  identifier
- **AND** the page MUST not render duplicated title/subtitle blocks or unrelated
  discover/visibility sections

#### Scenario: Unknown user shows branded not-found state

- **WHEN** a visitor opens `/users/{username}` and no public user exists for that
  username
- **THEN** the page MUST render a branded not-found state
- **AND** the state MUST give the visitor a way to recover to a known site
  destination
- **AND** no private lookup error details MUST be exposed

### Requirement: Public team profile exposes team name, members, captain label, and tournaments
The system SHALL provide a public `/teams/{teamname:string}` route that renders a privacy-safe team profile response with team name, members, captain identity, and participating tournaments while excluding invites. Public rosters MUST render member usernames as non-navigable text.

#### Scenario: Team page is public and excludes invites
- **WHEN** an anonymous visitor opens `/teams/{teamname}` for a known team
- **THEN** the page shows the team name
- **AND** the page shows team members by public username
- **AND** the page does not show team invites
- **AND** the page does not require authentication

#### Scenario: Team page labels captain inline
- **WHEN** a visitor opens `/teams/{teamname}` for a known team that has a captain
- **THEN** the captain appears in the roster as a normal member
- **AND** the captain member includes a clean Captain label
- **AND** the page does not render the captain as a separate roster row or standalone captain entry

#### Scenario: Team page lists participating tournaments
- **WHEN** a visitor opens `/teams/{teamname}` for a known team that is participating in tournaments
- **THEN** the page shows a "Playing in" section
- **AND** each listed tournament links to its game detail page

#### Scenario: Team member click does not open a public user profile
- **WHEN** a visitor selects a team member on a public team page
- **THEN** the member is rendered as username text without a `/users/{username}` link
- **AND** the captain may be identified inline

#### Scenario: Team profile uses focused branded layout
- **WHEN** a visitor opens `/teams/{teamname}` for a known team
- **THEN** the page presents the profile as branded roster and tournament surfaces
- **AND** the page does not render duplicated title/subtitle blocks
- **AND** the page does not render unrelated discover or visibility sections

#### Scenario: Unknown team shows branded not-found state
- **WHEN** a visitor opens `/teams/{teamname}` and no public team exists for that team name
- **THEN** the page renders a branded not-found state
- **AND** the state gives the visitor a way to recover to a known site destination
- **AND** no private lookup error details are exposed

### Requirement: Tournament participant list identities are username-only
The tournament participant list SHALL render user identities by username only and MUST NOT open a user-detail popup from a participant-list entry.

#### Scenario: Visitor selects a participant-list identity
- **WHEN** a visitor selects a team or user in a tournament participant list
- **THEN** the user identity is rendered as username text without a `/users/{username}` link
- **AND** no specific user-detail popup opens

#### Scenario: Team captain is labeled inline in participant list
- **WHEN** a visitor views a team participant in a tournament participant list
- **THEN** the captain appears in the member list with a Captain label
- **AND** no separate captain row or duplicate captain entry is rendered

### Requirement: Participant surfaces link only returned public identifiers
Tournament participant surfaces SHALL display user identities by username only and MUST NOT link them to `/users/{username}`; team name links MAY continue to use a returned team name and MUST NOT be discovered through an extra endpoint.

#### Scenario: Public username is present
- **WHEN** a public participant surface renders an individual participant or team member with a returned username
- **THEN** that username is rendered as text without a `/users/{username}` link
- **AND** the surface does not call a profile, current-user, or admin endpoint to discover a missing username

#### Scenario: Public username is missing
- **WHEN** a public participant surface renders an individual participant or team member without a returned username
- **THEN** the surface renders the available public display label without a user profile link
- **AND** the surface does not show private-field placeholders or hidden-field copy

#### Scenario: Public team name is present
- **WHEN** a public participant surface renders a team participant with a returned team name
- **THEN** the team name links to `/teams/{teamName}`
- **AND** the team member list continues to use only the member data returned in the loaded response

#### Scenario: Public team name is missing
- **WHEN** a public participant surface renders a team participant without a returned team route name
- **THEN** the surface renders the available public team display label without a team profile link
- **AND** the surface does not call a team profile or admin team endpoint to discover a missing route name

### Requirement: Public profile services use the current public resource contracts
Admin user-detail and public participant pages SHALL load their data through the versioned resource
defined for each role: the public user resource returns the username only, the admin user resource
returns the detailed record, and private current-user resources stay out of anonymous profile flows.

#### Scenario: Public user lookup is loaded
- **WHEN** the front-end loads a public user lookup
- **THEN** it requests `/v1/lan/public/users/{username}`
- **AND** it renders only the username returned by that resource

#### Scenario: Admin user-detail lookup
- **WHEN** an admin opens `/users/{username}`
- **THEN** the front-end requests the admin user resource at `/v1/lan/users/{username}`

#### Scenario: Admin opens user match summaries
- **WHEN** an admin opens `/users/{username}`
- **THEN** the front-end requests match summaries from `/v1/lan/users/{username}/match-summaries`

#### Scenario: Public team profile is loaded
- **WHEN** a visitor opens `/teams/{teamName}`
- **THEN** the front-end requests `/v1/lan/public/teams/{teamName}`
- **AND** it renders only the returned team name, public members, captain identity, logo, and
  participating tournament records
- **AND** it does not call authenticated team summary, invite, or admin endpoints to enrich the page

### Requirement: Public team tournament links use canonical tournament identifiers
Public team profile surfaces SHALL map participating tournament records through `TournamentId` and
use the canonical tournament-detail presentation route.

#### Scenario: Team lists a participating tournament
- **WHEN** a public team response contains a tournament record
- **THEN** the UI displays its returned tournament name
- **AND** the link targets `/tournaments/{TournamentId}`
- **AND** the UI does not expect a legacy `GameId` field or issue a tournament lookup per row

#### Scenario: Tournament identifier is unavailable
- **WHEN** a participating tournament record has no usable public tournament identifier
- **THEN** the UI omits the broken link while retaining any safe display label
- **AND** it does not substitute a private or inferred identifier

### Requirement: Public profile journeys connect returned context

Public user and team profiles SHALL expose related tournament, team, and match
summary context only when the current public response provides it, using the
canonical existing routes and preserving omission behavior for unavailable data.

#### Scenario: Public profile has related match context

- **WHEN** a public profile response includes a previous or upcoming match
  summary
- **THEN** the page MUST identify the tournament and match context and link to
  the canonical tournament detail destination when available
- **AND** it MUST not request private registration or account data to complete
  the summary

#### Scenario: Public profile has no related context

- **WHEN** the public response contains no related teams, tournaments, or match
  summaries
- **THEN** the profile MUST render a clear empty or omitted state
- **AND** the identity and recovery navigation MUST remain usable

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
