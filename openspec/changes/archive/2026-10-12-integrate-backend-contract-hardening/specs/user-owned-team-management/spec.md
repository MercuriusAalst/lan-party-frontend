## ADDED Requirements

### Requirement: Team mutations recover from a team_changed conflict

The front end MUST recover from a team mutation that the back end rejects with HTTP `409` and the
exact error code `team_changed` by showing localized conflict feedback, reloading the confirmed team
summary, and refreshing notifications. It MUST NOT automatically resend the mutation. A failure while
reloading the summary or refreshing notifications MUST be contained in a durable inline recovery
notice that keeps the localized conflict feedback visible, retains the last confirmed team state, and
offers a retry, so the page remains usable. This requirement prescribes no new notification-area UI.

#### Scenario: team_changed conflict during a mutation

- **WHEN** a team mutation is rejected with HTTP `409` and the `team_changed` code
- **THEN** the UI MUST show localized conflict feedback
- **AND** it MUST reload the confirmed team summary
- **AND** it MUST NOT resend the rejected mutation

#### Scenario: Conflict summary reload fails

- **WHEN** the confirmed-summary reload after a `team_changed` conflict fails
- **THEN** the conflict feedback MUST remain visible
- **AND** the page MUST remain usable with its last known state
- **AND** the user MUST be able to retry the reload

#### Scenario: Conflict notification refresh fails

- **WHEN** the notification refresh after a `team_changed` conflict fails
- **THEN** the failure MUST be contained and recoverable using the existing UI
- **AND** the conflict feedback and team summary MUST remain available

#### Scenario: Conflict recovery leaves authorization unchanged

- **WHEN** the `team_changed` conflict recovery runs for the current user
- **THEN** it MUST NOT change the user's authentication or authorization state
- **AND** it MUST keep any sign-in or permission explanation already shown
