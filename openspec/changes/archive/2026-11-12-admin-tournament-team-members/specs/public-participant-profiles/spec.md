## MODIFIED Requirements

### Requirement: Public team profile exposes team name, members, captain label, and tournaments
The system SHALL provide a public `/teams/{teamname:string}` route that renders a privacy-safe team profile response with team name, members, captain identity, and participating tournaments while excluding invites. Public rosters MUST render member usernames as non-navigable text for visitors without the administrator role; administrators MAY follow a member to the admin-only `/users/{username}` page.

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
- **WHEN** an anonymous visitor or authenticated non-admin selects a team member on a public team page
- **THEN** the member is rendered as username text without a `/users/{username}` link
- **AND** the captain may be identified inline

#### Scenario: Administrator opens a member profile from the team page
- **WHEN** an administrator views a known team on `/teams/{teamname}`, for example after selecting it in the global search
- **THEN** each member with a returned username links to `/users/{username}` with the username escaped
- **AND** the link has an accessible name that identifies the member
- **AND** a member without a returned username is rendered as text without a link

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

### Requirement: Participant surfaces link only returned public identifiers
Tournament participant surfaces SHALL display user identities by username only and MUST NOT link them to `/users/{username}` for visitors without the administrator role; team name links MAY continue to use a returned team name and MUST NOT be discovered through an extra endpoint. Administrator-only participant surfaces MAY link a returned username to the admin-only `/users/{username}` page.

#### Scenario: Public username is present
- **WHEN** a public participant surface renders an individual participant or team member with a returned username for a viewer without the administrator role
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

#### Scenario: Administrator opens player details from a tournament
- **WHEN** an administrator activates an individual participant card or a roster member with a returned username on a tournament page
- **THEN** the player details are shown in a dialog on the same page, loaded by that username from the administrator user endpoint
- **AND** no player details are requested for a participant without a returned username
