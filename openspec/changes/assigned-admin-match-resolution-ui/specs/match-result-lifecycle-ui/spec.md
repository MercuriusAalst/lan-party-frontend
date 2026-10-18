## MODIFIED Requirements

### Requirement: State-aware match details

The match details UI MUST show the backend lifecycle state, the current scores/reports, and any server-provided confirmation or correction deadline. A client countdown MUST be advisory and MUST NOT transition a match locally. The UI MUST NOT require either side to confirm the match ended before reporting a score.

#### Scenario: Match is awaiting a score

- **WHEN** an eligible participant or captain opens a match that the backend reports as awaiting ended confirmation, awaiting a score, or reversed
- **THEN** the score form MUST be available to eligible participants
- **AND** the UI MUST NOT offer a separate match-end confirmation action
- **AND** it MUST NOT show a per-side ended-confirmation status

#### Scenario: First score submitted

- **WHEN** the backend reports a saved first score report
- **THEN** the UI MUST explain the server deadline for the other side
- **AND** it MUST NOT show a redundant confirmation action for the submitting side

#### Scenario: Awaiting-report description is shared

- **WHEN** the backend reports awaiting ended confirmation or awaiting a score
- **THEN** the UI MUST show one description stating that either side may report the score
- **AND** it MUST state that the other side then has five minutes to agree or report a correction, otherwise the match goes to the assigned tournament administrator to resolve
- **AND** it MUST NOT promise that the reported score is accepted automatically

#### Scenario: Score-confirmation description is perspective-neutral

- **WHEN** the backend reports a score under confirmation
- **THEN** the description MUST be correct for both the reporting side and the other side
- **AND** it MUST state that the side that has not reported yet has five minutes to agree or report a correction, otherwise the match goes to the assigned tournament administrator to resolve
- **AND** it MUST NOT promise that the reported score is accepted automatically

#### Scenario: Admin resolution states why the admin is needed

- **WHEN** the backend reports admin resolution required because both sides reported conflicting scores
- **THEN** the status description MUST state that the reports conflict and that the assigned tournament administrator must resolve the result

#### Scenario: Admin resolution after a missing opponent report

- **WHEN** the backend reports admin resolution required because the opponent did not report a score before the window closed
- **THEN** the status description MUST state that the opponent did not report and that the assigned tournament administrator must resolve the result
- **AND** it MUST NOT present the lone report as the official result

## ADDED Requirements

### Requirement: Admin queue of matches awaiting resolution

The UI MUST give administrators an in-app list of matches that wait for an admin decision, because no email or push notification is sent. The list MUST come from the admin-only `GET /v1/lan/matches/pending-resolution` resource, which the backend limits to matches in tournaments in progress that were assigned to the signed-in administrator (and, for legacy tournaments without an assigned admin, to every administrator). The navigation MUST show an admin-only entry to the list with a count badge of the waiting matches. The badge MUST refresh when the navigation loads for an administrator and every 30 seconds while the interactive circuit is alive, MUST stop refreshing when the circuit or the administrator session ends, MUST keep its last known count when a refresh fails, and MUST NOT show an error for a failed refresh. A refresh MUST NOT run during prerendering or for a non-administrator.

#### Scenario: Administrator sees the waiting count

- **WHEN** the signed-in administrator has matches waiting for a decision
- **THEN** the admin navigation MUST show a badge with their number
- **AND** the badge MUST disappear when none are waiting

#### Scenario: Non-administrators have no entry

- **WHEN** the signed-in user is not an administrator
- **THEN** the navigation MUST NOT show the entry or badge
- **AND** the UI MUST NOT call the pending-resolution resource

#### Scenario: Badge refresh is quiet and bounded

- **WHEN** a badge refresh fails
- **THEN** the navigation MUST keep the last known count and show no error
- **AND** when the circuit is disposed the periodic refresh MUST stop

#### Scenario: Administrator opens the list

- **WHEN** an administrator opens `/admin/match-resolutions`
- **THEN** the page MUST list each waiting match with its tournament, both participants, the reported scores when present, the reason (conflicting reports, or the opponent did not report) and since when it waits
- **AND** it MUST omit the since-when text when the backend does not provide `RequiredSinceUtc`
- **AND** each item MUST offer a button that opens the existing match details dialog to resolve it

#### Scenario: Row states the assignment

- **WHEN** the backend provides an `Assignment` for a waiting match
- **THEN** the row MUST show a localized label: assigned to the signed-in admin, no assigned admin, the assigned admin is no longer an administrator, or escalated (waiting more than about ten minutes and shown to all admins)
- **AND** the escalated and assigned-admin-unavailable labels MAY use the existing warning styling, while the other labels MUST use neutral styling
- **AND** WHEN the backend omits `Assignment` or sends an unknown value THEN the row MUST show no assignment label and MUST still be listed

#### Scenario: Queue is ordered by urgency

- **WHEN** the list contains escalated or assigned-admin-unavailable matches
- **THEN** those matches MUST be listed before the others
- **AND** within each group the matches MUST be ordered by `RequiredSinceUtc`, oldest first, with matches without a known time last
- **AND** the navigation count MUST remain the number of all listed matches

#### Scenario: List states are visible

- **WHEN** the list loads, is empty, fails to load, or is rejected as unauthorized or forbidden
- **THEN** the page MUST show a loading state, an empty state, a retryable error state, or an unauthorized state respectively

#### Scenario: Resolved match leaves the list

- **WHEN** the administrator resolves or otherwise finishes a match from the dialog and closes it
- **THEN** the page MUST reload the list so the match is no longer shown

### Requirement: Mock backend mirrors admin resolution

The mock backend MUST NOT accept a lone score report automatically when the confirmation window closes. It MUST move such a match, and a conflict whose correction window closed, to admin resolution required. The mock MUST expose the same pending-resolution list for the admin persona, limited like the live resource, and MUST reject other personas.

#### Scenario: Mock window closes after one report

- **WHEN** the mock confirmation window closes and only one side reported
- **THEN** the mock match MUST become admin resolution required
- **AND** it MUST appear in the admin persona's pending-resolution list with the missing-opponent-report reason

#### Scenario: Mock conflict window closes

- **WHEN** the mock correction window closes on conflicting reports
- **THEN** the mock match MUST appear in the admin persona's pending-resolution list with the disputed reason

#### Scenario: Mock states the assignment

- **WHEN** the mock lists a waiting match
- **THEN** it MUST return assigned or unassigned like the live resource, and escalated once the recorded required-since time is older than ten minutes
- **AND** it MUST show a match assigned to another admin only once it is escalated
