# frontend-tournament-schedule-contract Specification

## Purpose
Defines the frontend tournament schedule contract for admin create/update forms, API schedule payloads, frontend planned/estimated timing models, visitor-facing schedule displays, unsupported bracket handling, and mock mode parity.
## Requirements
### Requirement: Admin tournament forms collect required schedule configuration

The frontend SHALL require admins to provide planned tournament schedule configuration when creating or updating a scheduled tournament.

#### Scenario: Admin creates a scheduled tournament
- **WHEN** an admin opens the create tournament form
- **THEN** the form includes controls for planned tournament start time, average single-game duration, and break duration between rounds
- **AND** the planned tournament start time control lets admins select a date and 24-hour time from picker controls without relying on AM/PM input
- **AND** the form prevents submission when planned start time is missing or duration values are missing, zero, or negative

#### Scenario: Admin updates an editable scheduled tournament
- **WHEN** an admin edits a tournament that remains in the scheduled editable state
- **THEN** the edit form shows the existing planned start time, average single-game duration, and round break duration values
- **AND** the admin can submit changed values with the rest of the editable tournament fields

#### Scenario: Backend rejects schedule values
- **WHEN** the backend rejects a create or update submission because schedule configuration is invalid or no longer editable
- **THEN** the frontend surfaces the backend validation error to the admin
- **AND** the form preserves the values the admin submitted so they can correct the input without re-entering the entire tournament

### Requirement: Frontend create and update requests use backend schedule field names

The frontend SHALL serialize tournament schedule configuration in create and update multipart
requests using the current backend tournament contract.

#### Scenario: Create request is submitted
- **WHEN** the frontend submits a create tournament multipart request
- **THEN** the payload includes `Name`, `BracketType`, `Format`, `FinalsFormat`, `ParticipationMode`, `Image`, `TeamSize` when applicable, `PlannedStartTime`, `AverageGameDurationMinutes`, and `RoundBreakDurationMinutes`
- **AND** the planned start time is serialized using a UTC-compatible representation that matches the backend API contract
- **AND** the payload does not include the removed `RegisterFormUrl` field

#### Scenario: Update request is submitted
- **WHEN** the frontend submits an update tournament multipart request
- **THEN** the payload includes the current planned schedule values and `TeamSize` alongside the other editable tournament fields
- **AND** duration values are sent as positive minute values rather than formatted display strings
- **AND** the payload does not include the removed `RegisterFormUrl` field

#### Scenario: Backend rejects schedule or team-size values
- **WHEN** the backend rejects a create or update submission because schedule configuration, team size, or editability is invalid
- **THEN** the frontend surfaces the backend validation error to the admin
- **AND** the form preserves the values the admin submitted so they can correct the input without re-entering the entire tournament

### Requirement: Frontend models distinguish planned and estimated schedule values

The frontend SHALL model the current tournament and match response fields with explicit names
instead of treating lifecycle timestamps as generated schedule estimates.

#### Scenario: Tournament list or detail response includes schedule fields
- **WHEN** the frontend deserializes a tournament response
- **THEN** it can read `PlannedStartTime`, `AverageGameDurationMinutes`, `RoundBreakDurationMinutes`, `EstimatedEndTime`, and optional `TeamSize` from explicit properties
- **AND** `StartTime` and `EndTime` remain available only for actual lifecycle timing if the backend returns them
- **AND** no external registration URL is required for the response to render

#### Scenario: Match response includes estimated timing
- **WHEN** the frontend deserializes a match in a tournament detail response
- **THEN** it can read `EstimatedStartTime`, `EstimatedEndTime`, and `TournamentId`
- **AND** the UI does not label those values as actual started or completed times
- **AND** it does not require a legacy `GameId` property

#### Scenario: Estimates have not been generated yet
- **WHEN** a scheduled tournament has no generated match estimates yet
- **THEN** the frontend treats estimated match and tournament end values as unavailable
- **AND** public schedule UI continues to show planned tournament configuration when that data is available

#### Scenario: API datetime values are received
- **WHEN** the frontend deserializes DateTime values from API-backed responses
- **THEN** the frontend converts those values to the local timezone before models are consumed by UI components
- **AND** outbound DateTime values remain serialized in a UTC-compatible representation when sent back to the backend

#### Scenario: Schedule values are edited and displayed
- **WHEN** the frontend displays schedule DateTime values
- **THEN** the UI formats them using local time
- **AND** datetime input and picker controls use local DateTime values while editing
- **AND** create and update submissions convert those local input values to UTC ISO strings with a `Z` suffix before sending them to the backend

### Requirement: Public timing displays label generated values as estimates

The frontend SHALL distinguish planned schedule values, generated estimates, and actual lifecycle timestamps in visitor-facing displays.

#### Scenario: Visitor views planned tournament timing
- **WHEN** a tournament has a planned start time before match generation
- **THEN** the UI labels the value as planned or scheduled timing rather than an actual start timestamp
- **AND** average game duration and round break duration are not shown in public schedule or registration displays because they are internal estimation inputs

#### Scenario: Visitor views generated match timing
- **WHEN** a match has estimated start and end times
- **THEN** the upcoming matches UI labels the visible start timing as `Start time`
- **AND** match detail popups opened from schedule or bracket views show the same local start datetime
- **AND** the schedule can order matches by estimated start time without requiring extra per-match API calls

#### Scenario: Visitor views actual lifecycle timing
- **WHEN** actual tournament start or end timestamps are shown alongside planned or estimated values
- **THEN** labels make the lifecycle status clear so visitors do not confuse actual timestamps with estimates

### Requirement: Frontend handles unsupported bracket types safely

The frontend SHALL avoid presenting unsupported bracket types as usable tournament options or broken bracket pages.

#### Scenario: Admin chooses a bracket type in frontend forms
- **WHEN** the frontend renders create or edit bracket type options
- **THEN** only frontend-supported bracket types are presented as selectable options
- **AND** unsupported backend-only bracket types are not introduced as partially supported choices

#### Scenario: API returns an unsupported bracket type
- **WHEN** a game response contains a bracket type the frontend cannot render
- **THEN** the frontend shows a clear unsupported or unavailable state for bracket-specific content
- **AND** non-bracket tournament details remain accessible when possible

### Requirement: Mock mode mirrors the schedule contract

The frontend mock backend SHALL include the same planned, estimated, and tournament-identity fields
used by API-backed mode and SHALL not depend on the removed external registration URL.

#### Scenario: Mock tournaments are loaded
- **WHEN** the application runs against mock data
- **THEN** mock tournament records include planned start time, schedule duration configuration, optional team size, and estimated end values consistent with the frontend models
- **AND** mock match records include estimated start and end values and `TournamentId` when match estimates are expected

#### Scenario: Mock create or update is submitted
- **WHEN** a mock create or update request includes schedule fields or team size
- **THEN** mock handling stores and returns those values using the same frontend model fields as API-backed mode
- **AND** mock handling does not require or persist `RegisterFormUrl`

### Requirement: Create tournament form initializes bound participation before validation

The frontend SHALL initialize the create tournament form's bound participation value to the individual (solo) option before the first render and the first validation pass, so that the option the form presents as selected is the value that is validated and submitted.

#### Scenario: Admin submits an untouched create form

- **WHEN** an admin opens the create tournament form and submits it without changing the participation control
- **THEN** the bound participation value MUST already be individual (solo)
- **AND** submission MUST NOT be rejected for a missing participation mode
- **AND** the admin MUST NOT have to toggle the participation control away from and back to its visible value

#### Scenario: Participation is bound before the first render

- **WHEN** the create tournament form is first rendered
- **THEN** the bound create request MUST report individual participation before any user interaction
- **AND** the visible selection and the bound value MUST agree

### Requirement: Create tournament failures show safe localized feedback

When a create tournament request fails, the frontend SHALL show a localized, user-facing message and MUST NOT render the raw API response body, including `ProblemDetails` JSON. Specific, safe validation feedback MAY be shown for a client error only when the backend supplies it in a recognized structured field such as `message`, `detail`, or a validation-error entry; all other bodies, server errors, and internal implementation details MUST be replaced by a localized generic message.

#### Scenario: Backend returns an internal server error

- **WHEN** creating a tournament fails with a server error whose response body is `ProblemDetails` or another JSON payload
- **THEN** the dialog MUST show a localized generic failure message
- **AND** the raw response body MUST NOT be rendered in the dialog or in any notification

#### Scenario: Backend returns specific validation feedback

- **WHEN** creating a tournament fails with a client error that carries a specific, safe validation message in a recognized structured field such as `message`, `detail`, or a validation-error entry
- **THEN** that validation message MAY be shown to the admin
- **AND** the shown text MUST be user-facing copy rather than a serialized payload

#### Scenario: Client error body is unstructured

- **WHEN** creating a tournament fails with a client error whose body is plain text, a JSON string, a JSON array, or a JSON object without a recognized message, detail, or validation-error field
- **THEN** the dialog MUST show the localized generic failure message
- **AND** the response body MUST NOT be rendered in the dialog or in any notification

#### Scenario: Failure is visible and accessible

- **WHEN** a create failure message is shown
- **THEN** it MUST be rendered as a visible alert near the top of the create form
- **AND** it MUST be exposed to assistive technology as an alert
- **AND** it MUST be visually distinct from ordinary helper text

#### Scenario: Form state survives a create failure

- **WHEN** a create failure message is shown
- **THEN** the values the admin entered MUST remain in the form
- **AND** the admin MUST be able to correct the input and retry without re-entering the whole tournament

