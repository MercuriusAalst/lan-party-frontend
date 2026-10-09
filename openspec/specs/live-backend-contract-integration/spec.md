# live-backend-contract-integration Specification

## Purpose
TBD - created by archiving change integrate-current-backend-contracts-2026-08-28. Update Purpose after archive.
## Requirements
### Requirement: Live Refit calls use the versioned canonical API boundary

The reorganized front end MUST continue to send live API requests through the
existing Refit clients with a single `/v1` base path and the exact current
back-end route, verb, query, body, and multipart field names. Product-level
journey composition MUST NOT call removed aliases, construct a second ad-hoc
HTTP client, or invent a presentation-specific endpoint.

#### Scenario: Public tournament browse request

- **WHEN** the front end loads the tournament browse surface in live mode
- **THEN** it MUST send `GET /v1/lan/tournaments` with optional `page` and
  `pageSize` query values
- **AND** the journey composition MUST consume the existing response projection

#### Scenario: Public tournament detail request

- **WHEN** the front end loads a tournament detail page in live mode
- **THEN** it MUST send `GET /v1/lan/tournaments/{tournamentId}`
- **AND** it MUST not send `GET /v1/lan/games/{gameId}`

#### Scenario: Canonical GUID route uses the existing API identifier

- **WHEN** the canonical GUID tournament detail route is opened
- **THEN** the front end MUST use the route GUID as the existing tournament
  identifier before calling the detail API
- **AND** the live request MUST keep the existing `tournamentId` path value and
  MUST NOT require a name/slug route or lookup endpoint

#### Scenario: Removed game route is not used

- **WHEN** the live API client contract is inspected or exercised
- **THEN** no Refit method MUST target `/lan/games`
- **AND** no fallback request MUST be attempted when a canonical tournament
  call fails

#### Scenario: Reorganized journey reaches a live action

- **WHEN** a player or organizer selects an existing API-coupled action from a
  reorganized surface
- **THEN** the front end MUST call the same existing Refit/service operation
  with its current contract
- **AND** no journey-only endpoint or DTO field MAY be required for success

### Requirement: Tournament administration mirrors current resource contracts
The front-end MUST represent the current tournament create, update, sponsor, delete, and lifecycle
resources, including multipart schedule fields, `TeamSize`, the optional assigned contact
administrator, and the optional top-three prizes, and MUST preserve admin-only authorization
expectations.

#### Scenario: Admin creates or updates a tournament
- **WHEN** an authenticated admin submits a tournament form
- **THEN** the live request targets `POST /v1/lan/tournaments` or `PATCH /v1/lan/tournaments/{tournamentId}`
- **AND** multipart data contains `Name`, `BracketType`, `Format`, `FinalsFormat`, `ParticipationMode`, `Image` when selected, `TeamSize` when applicable, `PlannedStartTime`, `AverageGameDurationMinutes`, and `RoundBreakDurationMinutes`
- **AND** multipart data carries `AssignedAdminUserId`, `FirstPlacePrize`, `SecondPlacePrize`, and `ThirdPlacePrize`, sending an explicit empty value for a cleared contact administrator or an empty prize so the backend can clear a previously set value
- **AND** multipart data does not contain the removed `RegisterFormUrl` field

#### Scenario: Admin selects a contact administrator
- **WHEN** an authenticated admin opens the contact-administrator picker
- **THEN** the front-end requests `GET /v1/lan/users/admins` with optional `query` and `pageSize` values
- **AND** it lists only the returned validated administrators using the public-safe `{id, username, displayName}` projection
- **AND** it does not fall back to the general user search resource to populate the picker

#### Scenario: Administrator list is unavailable
- **WHEN** the administrator list resource returns an authorization, configuration, or availability failure
- **THEN** the picker MUST present a recoverable failure state instead of an empty selectable list
- **AND** the form MUST remain editable and MUST surface the failure when the admin retries

#### Scenario: Admin changes lifecycle state
- **WHEN** an authenticated admin starts, resets, completes, or cancels a tournament
- **THEN** the front-end sends `PUT /v1/lan/tournaments/{tournamentId}/lifecycle-state` with a supported `state`
- **AND** it does not call the removed `/start`, `/reset`, `/complete`, or `/cancel` action routes

#### Scenario: Admin replaces a tournament sponsor
- **WHEN** an authenticated admin saves the selected tournament sponsor
- **THEN** the front-end sends `PUT /v1/lan/tournaments/{tournamentId}/sponsors` with zero or one `sponsorPlacements` entry
- **AND** the response is treated as the current tournament detail projection

### Requirement: Tournament registration uses backend-authoritative resources
The front-end MUST expose current registration state and call the current self-registration,
eligibility, roster, confirmation, and admin-removal resources. It MUST NOT model arbitrary admin
participant additions or the removed external registration URL as live capabilities.

#### Scenario: Authenticated user registers individually
- **WHEN** an authenticated user registers for an eligible individual tournament
- **THEN** the front-end may check `GET /v1/lan/tournaments/{tournamentId}/registrations/individual/eligibility`
- **AND** it submits `PUT /v1/lan/tournaments/{tournamentId}/registrations/individual/me`
- **AND** it refreshes current-user registration state after success

#### Scenario: Authenticated user unregisters individually
- **WHEN** an authenticated user withdraws from an eligible individual tournament
- **THEN** the front-end sends `DELETE /v1/lan/tournaments/{tournamentId}/registrations/individual/me`
- **AND** it does not send a user-id path or an admin-add/remove action for self withdrawal

#### Scenario: Team captain submits and confirms a roster
- **WHEN** a team captain manages a team tournament registration
- **THEN** the front-end uses team eligibility and roster-eligibility resources before mutation when appropriate
- **AND** it submits `PUT /v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}/roster` with bounded unique `userIds`
- **AND** selected members can confirm through `PATCH /v1/lan/tournaments/{tournamentId}/registrations/roster-members/{rosterMemberId}` with `confirmationStatus` set to `Confirmed`

#### Scenario: Current-user registration state is loaded
- **WHEN** an authenticated user opens a tournament detail page
- **THEN** the front-end may request `GET /v1/lan/tournaments/{tournamentId}/registrations/me`
- **AND** it uses the returned `CanRegisterIndividual`, `CanConfirmRoster`, `CanUnregister`, and captain-managed registration state to choose controls
- **AND** an anonymous visitor does not call this authenticated resource

#### Scenario: Admin removes a registration
- **WHEN** an authenticated admin removes a tournament registration
- **THEN** the front-end uses `DELETE /v1/lan/tournaments/{tournamentId}/registrations/admin/users/{userId}` or `/teams/{teamId}` with a reason body
- **AND** it does not offer an admin endpoint to add participants, swap roster members, or force confirmation

### Requirement: Live response models preserve privacy-safe projections and canonical identifiers
The front-end MUST deserialize the current tournament, match, registration, team, search, sponsor, and
profile response shapes without requiring private-field enrichment calls. Aggregate-level identifiers
MUST use `TournamentId`; genuine format and duration fields MUST remain unchanged.

#### Scenario: Tournament detail contains active public registrations
- **WHEN** an anonymous visitor receives a tournament detail response
- **THEN** the front-end reads active public registrations and their privacy-safe user/team/roster projections
- **AND** pending confirmation state, email, Auth0 identifiers, deletion state, timestamps, and other private account fields are not required for rendering

#### Scenario: Match references its tournament
- **WHEN** a match is read from tournament detail or match detail
- **THEN** the front-end reads `tournamentId`
- **AND** it does not require a `gameId` JSON property

#### Scenario: Public team profile references a tournament
- **WHEN** an anonymous visitor reads a public team profile
- **THEN** each participating tournament record is read from `tournamentId` and `name`
- **AND** the UI can map that identifier to the public `/tournaments/{id}` presentation route

#### Scenario: Global search returns tournament records
- **WHEN** a valid public search completes
- **THEN** the front-end reads a wrapper containing `results`, `nextCursor`, and `hasMore`
- **AND** result types include `user`, `team`, or `tournament`
- **AND** tournament records use `tournamentId` for navigation

### Requirement: Mock mode mirrors the live integration semantics
Mock mode MUST continue to implement the same front-end service interfaces and MUST provide
representative tournament, registration, team, search, sponsor, profile, loading, empty, success,
and blocked-action states using the current public and authenticated response shapes.

#### Scenario: Mock tournament detail is loaded
- **WHEN** the application runs in mock mode and a visitor opens a tournament detail page
- **THEN** the response includes schedule fields, a singular or absent sponsor placement, privacy-safe active registrations, matches with `TournamentId`, and no required external registration URL

#### Scenario: Mock registration and team actions are exercised
- **WHEN** a mock user registers, submits a roster, confirms a roster member, manages a team invite, or changes tournament state
- **THEN** mock state updates through the same service-level workflow semantics as live mode
- **AND** representative validation, unauthorized, forbidden, not-found, and conflict states remain testable

#### Scenario: Mock search matches the live result contract
- **WHEN** a mock visitor searches with at least three trimmed characters
- **THEN** the mock service returns wrapped bounded results using `user`, `team`, and `tournament` types and `TournamentId`
- **AND** shorter queries return no results without a lookup

### Requirement: Mock mode mirrors reorganized journey semantics

Mock mode MUST continue to implement the same front-end service interfaces and
representative public/authenticated response states used by the reorganized
player, team, profile, tournament, sponsor, and organizer journeys.

#### Scenario: Mock action is selected from a new entry point

- **WHEN** a mock user reaches an existing registration, team, profile, sponsor,
  or organizer action through the reorganized UI
- **THEN** the mock service MUST receive the same logical operation and return
  the same success, blocked, unauthorized, conflict, or error semantics
- **AND** the UI MUST not require live-only fields or endpoints

