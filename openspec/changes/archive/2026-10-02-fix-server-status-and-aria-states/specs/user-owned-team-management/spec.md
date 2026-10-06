## ADDED Requirements

### Requirement: Invite dialog actions remain reachable during empty search

The invite dialog MUST keep its action controls reachable when the global-user search dropdown displays an empty result state. Each action MUST retain its existing enabled or disabled state.

#### Scenario: Captain cancels while no players match the search

- **WHEN** a captain opens the invite dialog and the search dropdown displays no matching players
- **THEN** the dialog action controls MUST remain visible and unobstructed
- **AND** selecting Cancel MUST close the dialog without sending an invitation
