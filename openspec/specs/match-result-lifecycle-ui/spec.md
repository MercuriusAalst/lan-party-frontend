# match-result-lifecycle-ui Specification

## Purpose
TBD - created by archiving change issue-44-match-result-lifecycle-ui. Update Purpose after archive.
## Requirements
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
- **AND** it MUST state that the other side then has five minutes to agree or report a correction, otherwise the reported score becomes final

#### Scenario: Score-confirmation description is perspective-neutral

- **WHEN** the backend reports a score under confirmation
- **THEN** the description MUST be correct for both the reporting side and the other side
- **AND** it MUST state that the side that has not reported yet has five minutes to agree or report a correction, otherwise the reported score becomes final

### Requirement: Consensus and dispute actions
The UI MUST allow an eligible participant/captain to submit a valid score only when the backend allows it. It MUST show pending confirmation, disputed, and admin-resolution-required states distinctly, including correction deadlines and an actionable refresh/error path. The UI MUST NOT claim a result is complete until a refreshed backend projection reports completion.

#### Scenario: Scores differ
- **WHEN** the backend reports a disputed score
- **THEN** the UI MUST show the differing reports and correction window
- **AND** it MUST allow eligible correction submission only while the backend state permits it

#### Scenario: Correction window expires
- **WHEN** the backend reports admin resolution required
- **THEN** normal score actions MUST be disabled
- **AND** admins MUST see a resolve action while other users see a clear explanation

#### Scenario: Protected action projection exposes both dispute reports
- **WHEN** an eligible participant or team captain opens a disputed match
- **THEN** the UI MUST show both authoritative reports from the protected action projection
- **AND** an unrelated authenticated viewer MUST see neither private report

#### Scenario: Assigned administrator reviews dispute reports
- **WHEN** the assigned tournament administrator opens a disputed or admin-resolution-required match before it is resolved
- **THEN** the UI MUST show both private reports with explicit participant labels
- **AND** an unassigned global administrator MUST retain eligible administrative actions even when private reports are withheld

### Requirement: Forfeit and administrative actions
The UI MUST require explicit confirmation before a participant or administrator forfeits, and before an administrator reverses a result. It MUST show actions only when the fresh protected action projection grants the corresponding capability, show admin resolution/force/reversal only to admins, and explain why an action is blocked when tournament state or linked downstream matches disallow it. An administrator assignment MAY scope private report visibility but MUST NOT suppress an eligible global-admin action. Team matches MUST identify captain-only actions.

#### Scenario: Participant forfeits
- **WHEN** an eligible participant confirms the forfeit dialog
- **THEN** the UI MUST invoke the backend forfeit command
- **AND** it MUST refresh the match and bracket before presenting the resulting state

#### Scenario: Reversal is unavailable
- **WHEN** the backend rejects reversal because downstream play has started
- **THEN** the UI MUST retain the current projection
- **AND** it MUST display the server-provided reason

### Requirement: Safe loading and error handling
The UI MUST show loading immediately when a match refresh or mutation begins, disable stale actions during that operation, and distinguish unauthorized, not-found, expired/invalid-state, and transient failures. A failed refresh after a successful mutation MUST be reported as “saved, but the displayed state could not be refreshed” while retaining the last known projection.

#### Scenario: Unauthorized viewer
- **WHEN** the backend rejects a protected action for the current identity
- **THEN** the UI MUST hide or disable the action and provide a sign-in/permission explanation

#### Scenario: Refresh failure after mutation
- **WHEN** a lifecycle command succeeds but the subsequent match refresh fails
- **THEN** the UI MUST communicate that the result was saved but the displayed state may be stale
- **AND** it MUST provide a retry action

#### Scenario: Tournament reload fails after a fresh child match refresh
- **WHEN** a match dialog refreshes the authoritative match successfully but the containing schedule or bracket reload fails
- **THEN** the parent MUST retain and propagate the fresh same-ID match projection
- **AND** the dialog MUST NOT replace its fresh action state with the stale parent parameter

#### Scenario: Protected refresh is forbidden
- **WHEN** the protected action refresh is unauthorized or forbidden
- **THEN** the UI MUST fetch and render the public match projection
- **AND** it MUST keep all mutations disabled while showing a distinct sign-in or permission explanation

#### Scenario: Public fallback refresh succeeds
- **WHEN** a lifecycle command is followed by a protected refresh that returns unauthorized but the public match refresh succeeds
- **THEN** the UI MUST propagate the fresh public match projection to the containing schedule and bracket
- **AND** it MUST keep protected actions disabled while showing the public-state limitation

#### Scenario: Bracket refresh reaches the wrapper before close
- **WHEN** a single- or double-elimination bracket dialog successfully refreshes a match and is then closed while the parent reload fails
- **THEN** the bracket wrapper and parent MUST retain the refreshed same-ID match projection
- **AND** they MUST NOT submit or render the wrapper's stale pre-refresh match

### Requirement: Mock/live parity

The mock service MUST expose the same lifecycle DTOs, command methods, validation outcomes, and state transitions as the live client for happy paths, duplicate actions, disputes, deadlines, forfeits, admin resolution, and reversal guards. The mock MUST NOT expose a match-end confirmation command or capability.

#### Scenario: Mock match follows lifecycle actions

- **WHEN** a participant or administrator uses the mock match service
- **THEN** the mock MUST apply the same score, dispute, forfeit, resolution, deadline, and reversal guards exposed by the live contract
- **AND** the mock MUST record a side's ended confirmation atomically with its score report instead of requiring a separate confirmation command
- **AND** the mock MUST return explicit lifecycle state instead of inferring completion from a winner or score alone

#### Scenario: Anonymous mock requests remain public

- **WHEN** an anonymous mock login or unauthenticated request asks for match action state
- **THEN** the mock service MUST resolve the request as anonymous rather than the default participant persona
- **AND** it MUST return the public match projection without private reports or lifecycle mutation capabilities

#### Scenario: Interactive mock services retain the authenticated persona

- **WHEN** an authenticated mock user or administrator invokes a service from an interactive circuit without an ambient HTTP context
- **THEN** the mock service MUST resolve the persona from the circuit authentication state
- **AND** it MUST preserve the corresponding profile, action, and private-report authorization behavior

### Requirement: Deadline transition triggers a bounded authoritative tournament refresh

An advisory client countdown MUST NOT change a match's state locally. When a refreshed backend match
projection (`GET /v1/lan/matches/{id}` or `/me`) confirms that a confirmation or correction deadline
has expired, the owning tournament page MUST run a bounded, cancellable, authoritative tournament
refresh that continues until the authoritative tournament aggregate and bracket converge on the
backend's effective projection. The refresh MUST NOT compute a winner, score, or bracket advancement
locally. A strictly newer authoritative same-ID match projection MUST supersede an older expected
projection, and a newer same-ID match projection already obtained from a fresh child refresh MUST be
retained when a later aggregate reload returns a stale or older projection. The refresh MUST survive
the match dialog being closed, MUST stop when the page navigates away or is disposed, and MUST NOT
let an out-of-order response overwrite newer state. When the refresh is exhausted or fails, the page
MUST show recoverable feedback in a durable inline notice that stays until the refresh succeeds or is
retried, and MUST offer a retry.

#### Scenario: Client countdown is advisory

- **WHEN** the advisory client countdown reaches zero
- **THEN** the UI MUST NOT change the match state locally
- **AND** it MUST wait for a refreshed backend match projection before treating the deadline as
  expired

#### Scenario: Refreshed backend projection confirms the expired deadline

- **WHEN** a refreshed backend match projection confirms the effective deadline has expired
- **THEN** the tournament page MUST start the bounded authoritative tournament refresh
- **AND** it MUST reconcile the match and bracket from the backend response

#### Scenario: Refresh continues until the bracket converges

- **WHEN** the deadline refresh returns a tournament aggregate or bracket that has not yet converged
  on the backend's effective projection
- **THEN** the page MUST continue the bounded refresh
- **AND** it MUST NOT compute a local winner or advancement

#### Scenario: Dialog closes before the refresh completes

- **WHEN** the match dialog is closed while the page-owned deadline refresh is still running
- **THEN** the page MUST continue the refresh
- **AND** it MUST apply the resulting authoritative projection to the page

#### Scenario: Stale aggregate reload after a fresh child refresh

- **WHEN** a fresh same-ID match projection has been applied and a later aggregate reload returns an
  older projection for that match
- **THEN** the page MUST retain the strictly newer match projection
- **AND** it MUST NOT replace it with the stale projection

#### Scenario: Out-of-order refresh response

- **WHEN** an older refresh response arrives after a newer refresh response
- **THEN** the page MUST keep the newer projection
- **AND** it MUST NOT apply the older response

#### Scenario: Downstream bracket slots converge from the backend

- **WHEN** the deadline refresh returns a bracket whose downstream slots carry server-assigned
  participants or sources
- **THEN** the page MUST render those authoritative slots
- **AND** it MUST NOT assign downstream participants or advancement locally

#### Scenario: Navigation or disposal stops the refresh

- **WHEN** the user navigates away from the tournament page or the page is disposed
- **THEN** the deadline refresh MUST be cancelled
- **AND** it MUST NOT apply a later response to the page

#### Scenario: Refresh exhausted or failed

- **WHEN** the bounded deadline refresh is exhausted or its request fails
- **THEN** the page MUST show recoverable feedback
- **AND** it MUST offer a retry that reruns the refresh

#### Scenario: Existing protected and public projection behavior is preserved

- **WHEN** the deadline refresh runs for an authenticated, forbidden, or anonymous viewer
- **THEN** it MUST keep the existing protected-action projection, public fallback, loading, and
  private-report visibility behavior unchanged

### Requirement: Match score entry follows the configured series format

The match details UI MUST explain the required wins for the configured Best-of format and the valid losing-score range using wording specific to that format. Player score submissions and administrator resolutions MUST be entered by choosing the winning participant and then the number of games the losing participant won, limited to the whole numbers from 0 up to one less than the required wins. The UI MUST set the winner's score to the required wins automatically and MUST NOT offer a free-text score entry. For a Best of 1 match the UI MUST require only the winner choice, MUST treat the loser's score as 0 and MUST NOT show the losing-score step. The UI MUST NOT preselect a winner unless the viewer's own earlier report or the official result is being shown. Score submission and resolution MUST stay disabled until the selection is complete, and the UI MUST show a live summary of the resulting score in the displayed participant order. The UI MUST still check the resulting score against the configured format before invoking the backend, while the backend remains authoritative. When the match format is unknown, the UI MUST NOT show score guidance, MUST NOT offer a score selection, and MUST NOT reference guidance from `aria-describedby`. The selection controls MUST be operable with the keyboard, MUST expose their checked state to assistive technology and MUST NOT rely on colour alone to show the selected option.

#### Scenario: Player picks a decisive score

- **WHEN** a player chooses a winner and, for a format longer than Best of 1, the loser's games won
- **THEN** the UI MUST allow submission
- **AND** it MUST submit the participant scores in their displayed order with the winner at the required wins

#### Scenario: Losing score options follow the format

- **WHEN** a winner is chosen in a Best of 3 match
- **THEN** the UI MUST offer exactly the losing scores 0 and 1
- **AND** in a Best of 5 match it MUST offer exactly 0, 1 and 2

#### Scenario: Best of 1 needs only the winner

- **WHEN** a player chooses a winner in a Best of 1 match
- **THEN** the UI MUST allow submission without a losing-score step
- **AND** it MUST submit the winner with 1 and the other participant with 0

#### Scenario: Selection is incomplete

- **WHEN** no winner is chosen, or a winner is chosen but the required losing score is not
- **THEN** the UI MUST disable score submission or resolution
- **AND** the handler MUST NOT invoke the backend

#### Scenario: Administrator resolves with the picker

- **WHEN** an administrator resolves a match
- **THEN** the UI MUST use the same winner and losing-score selection as the player form
- **AND** it MUST keep resolution disabled until the selection is complete

#### Scenario: Live result summary

- **WHEN** the selection is complete
- **THEN** the UI MUST show a summary of the result in the displayed participant order, such as "Alpha 2 – 1 Bravo", in a polite live region

#### Scenario: Format-specific guidance

- **WHEN** the match format is Best of 1, Best of 3 or Best of 5
- **THEN** the UI MUST show guidance worded for that format, such as "Best of 1: the winner has 1 win, the other side 0."

#### Scenario: Unknown match format

- **WHEN** the match format is not a supported Best-of format
- **THEN** the UI MUST NOT render score guidance or a score selection
- **AND** the score controls MUST NOT reference a missing guidance element through `aria-describedby`

### Requirement: Score validation errors are actionable and safe

The match details UI MUST display a nonempty backend validation message for HTTP 400 score-action responses when one is available. It MUST preserve the existing unauthorized, forbidden, and conflict mappings. For other failures, including HTTP 500, it MUST use the generic localized fallback and MUST NOT expose the response body.

#### Scenario: Backend rejects a score with a validation error

- **WHEN** a score action receives HTTP 400 with a user-facing validation message
- **THEN** the UI MUST show that message

#### Scenario: Backend failure has no safe validation message

- **WHEN** a score action receives a response without a supported validation message or a non-400 server failure
- **THEN** the UI MUST show its generic localized fallback
- **AND** it MUST NOT expose a server error body

### Requirement: Feedback toasts appear above open dialogs

Success and error toasts raised while a dialog is open MUST be rendered above that dialog and its overlay so they are visible and not obscured.

#### Scenario: Error toast while the match dialog is open

- **WHEN** a score action fails while the match details dialog is open
- **THEN** the toast MUST be visible above the dialog

### Requirement: Match dialog action buttons size to their content

The action buttons in the match details dialog, such as submit, resolve and forfeit, MUST be sized to their content instead of stretching across the full width of their container, and MUST wrap onto further lines when the available width is too small.

#### Scenario: Action buttons are content-width

- **WHEN** the match details dialog shows an action button
- **THEN** the button MUST NOT span the full width of the dialog panel at any viewport width

