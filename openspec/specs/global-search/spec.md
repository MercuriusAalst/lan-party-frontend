# global-search Specification

## Purpose
TBD - created by archiving change add-global-menu-search-public-profiles. Update Purpose after archive.
## Requirements
### Requirement: Global search uses the live public search contract
The system SHALL search users, teams, and tournaments through the live public search contract when
the trimmed query contains at least 3 characters, and matching eligibility and ordering SHALL
follow the back-end response.

#### Scenario: Fewer than 3 characters do not search
- **WHEN** a visitor enters a search value whose trimmed length is less than 3 characters
- **THEN** the system does not perform a global search lookup
- **AND** no user, team, or tournament results are returned for that value

#### Scenario: Three or more characters return live matches
- **WHEN** a visitor enters a search value whose trimmed length is at least 3 characters
- **THEN** users, teams, and tournaments returned by the live search response are eligible search results
- **AND** result ordering follows the deterministic relevance order returned by the live search response
- **AND** result matching is case-insensitive according to the live search contract

### Requirement: Global search returns normalized safe result records
The global search API SHALL return a response wrapper containing bounded normalized search result
records and pagination metadata, and each result SHALL identify the result type and provide only
the data needed to render a result and navigate to its public destination. User results MUST be
limited to the username and MUST NOT send non-admin visitors to the admin-only
`/users/{username}` user-detail route.

#### Scenario: Search response includes wrapper metadata
- **WHEN** a valid global search request completes
- **THEN** the response includes a `results` collection
- **AND** the response includes `nextCursor`
- **AND** the response includes `hasMore`

#### Scenario: Search response includes every supported result type
- **WHEN** a search query matches at least one user, one team, and one tournament
- **THEN** the response includes result records whose type identifies `user`, `team`, or `tournament`
- **AND** each record includes a display label suitable for the dropdown
- **AND** each record includes supporting text suitable for the dropdown
- **AND** each user result includes the username needed to render the result, and only an admin is
  sent to the admin-only `/users/{username}` user-detail page
- **AND** each team result includes the team name needed for `/teams/{teamname}`
- **AND** each tournament result includes the tournament id needed for the `/tournaments/{tournamentId}` presentation page

#### Scenario: Search response excludes private user and team fields
- **WHEN** the global search response includes user or team matches
- **THEN** user results do not include email, first name, last name, roles, timestamps, deletion state, Auth0 identifiers, platform identifiers, or admin/internal fields
- **AND** team results do not include team invites or private member/account fields

### Requirement: Global search failures are representable to consumers
The global search service SHALL make loading, empty, and error outcomes distinguishable so the
navigation dropdown can render the correct state.

#### Scenario: Search is pending
- **WHEN** a valid debounced search query is in flight
- **THEN** the consumer can identify that results are loading

#### Scenario: Search completes with no matches
- **WHEN** a valid search query completes without matching users, teams, or tournaments
- **THEN** the consumer can identify the empty result state

#### Scenario: Search fails
- **WHEN** a valid search query fails because the search service or API cannot complete the request
- **THEN** the consumer can identify the error state without treating stale previous results as current matches
