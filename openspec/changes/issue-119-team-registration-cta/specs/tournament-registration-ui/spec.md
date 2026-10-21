## ADDED Requirements

### Requirement: Captains can create a team from registration

The registration popup MUST give a captain without any teams they captain a clear path to create a
team, while preserving separate guidance for teams that exist but are too small.

#### Scenario: Captain has no teams

- **WHEN** the authenticated captain's team list has loaded successfully and contains no teams
- **THEN** the registration popup MUST show a centered, accessible empty-state card with a clear
  title and supporting explanation
- **AND** it MUST provide a prominent full-width `Create a team` action
- **AND** choosing the action MUST navigate to `/teams/manage` with a one-time intent to open the
  existing team creation dialog
- **AND** the create intent MUST be consumed so later renders or navigation do not reopen the dialog

#### Scenario: Captain has only undersized teams

- **WHEN** the captain has one or more teams but none meet the tournament's required member count
- **THEN** the popup MUST retain the required-member-count explanation
- **AND** it MUST provide a separate `Manage teams` action

#### Scenario: Captain creates or cancels a team

- **WHEN** the create dialog is opened from the registration empty state
- **THEN** creating a team MUST leave the user on normal team management so they can invite players
- **AND** cancelling MUST leave team and tournament-registration state unchanged
- **AND** neither outcome MUST resume the tournament or automatically register the team
