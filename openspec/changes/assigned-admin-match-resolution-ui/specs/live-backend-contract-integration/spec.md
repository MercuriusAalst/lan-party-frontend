## MODIFIED Requirements

### Requirement: Tournament administration mirrors current resource contracts
The front-end MUST represent the current tournament create, update, sponsor, delete, and lifecycle
resources, including multipart schedule fields, `TeamSize`, the mandatory assigned
administrator, and the optional top-three prizes, and MUST preserve admin-only authorization
expectations.

#### Scenario: Admin creates or updates a tournament
- **WHEN** an authenticated admin submits a tournament form
- **THEN** the live request targets `POST /v1/lan/tournaments` or `PATCH /v1/lan/tournaments/{tournamentId}`
- **AND** multipart data contains `Name`, `BracketType`, `Format`, `FinalsFormat`, `ParticipationMode`, `Image` when selected, `TeamSize` when applicable, `PlannedStartTime`, `AverageGameDurationMinutes`, and `RoundBreakDurationMinutes`
- **AND** multipart data always carries a non-empty `AssignedAdminUserId` (the backend rejects a missing admin on create and on update, including for legacy tournaments that have none)
- **AND** multipart data carries `FirstPlacePrize`, `SecondPlacePrize`, and `ThirdPlacePrize`, sending an explicit empty value for an empty prize so the backend can clear a previously set value
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

## ADDED Requirements

### Requirement: Pending match resolutions resource

The front-end MUST read the admin-only list of matches awaiting resolution from `GET /v1/lan/matches/pending-resolution` through `ILANClient` and the tournament service. Each item MUST be modelled as `PendingMatchResolutionDTO` with `MatchId`, `TournamentId`, `TournamentName`, `Participant1Name`, `Participant2Name`, the nullable reported scores `Participant1ReportedScore1`, `Participant1ReportedScore2`, `Participant2ReportedScore1` and `Participant2ReportedScore2`, `Reason` (`Disputed` or `MissingOpponentReport`, serialized as a string), the optional `Assignment` (`Assigned`, `Unassigned`, `AssignedAdminUnavailable` or `Escalated`, serialized as a string) and the nullable `RequiredSinceUtc`. A missing or unknown `Assignment` MUST be read as absent without failing the list. The reported scores are unconfirmed claims and MUST NOT be presented as a final result.

#### Scenario: Admin requests the pending list

- **WHEN** an administrator opens the queue or the navigation refreshes its badge
- **THEN** the front-end sends `GET /v1/lan/matches/pending-resolution`
- **AND** it maps the string `Reason`, a missing or unknown `Assignment` and a null `RequiredSinceUtc` without failing

#### Scenario: Backend no longer auto-accepts a lone report

- **WHEN** the score window closes after only one report
- **THEN** the front-end MUST treat the match as admin resolution required and MUST NOT expect an automatic result
