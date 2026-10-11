## MODIFIED Requirements

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
