## ADDED Requirements

### Requirement: Captains can create a team from registration

The registration popup MUST give a captain without any teams they captain a clear path to create a
team, while preserving separate guidance for teams that exist but are too small.

#### Scenario: Captain has no teams

- **WHEN** the authenticated captain's team list has loaded successfully and contains no teams
- **THEN** the registration popup MUST show a centered, rounded card with a subtle contrasting
  surface and comfortable padding
- **AND** the card MUST include a recognizable team or people icon, a bold centered title, and
  centered supporting text
- **AND** it MUST provide a prominent primary `Create a team` button with a plus icon and a
  suitable forward arrow
- **AND** the button MUST span the available card width on mobile and use only its content width,
  centered within the card, on wide screens
- **AND** choosing the action MUST navigate to `/teams/manage` with a one-time intent to open the
  existing team creation dialog
- **AND** the create intent MUST be consumed so later renders or navigation do not reopen the dialog
- **AND** an accepted invitation to a team the player does not captain MUST NOT count as a captained
  team

#### Scenario: Team summary is loading or unavailable

- **WHEN** team loading is in progress or the team summary request fails
- **THEN** the existing loading or error state MUST remain visible
- **AND** the empty-state card MUST NOT be shown

#### Scenario: Captain has an existing registration

- **WHEN** the captain already has a registration or a roster-selection decision
- **THEN** the existing registration status, roster confirmation details, and cancellation actions
  MUST remain available
- **AND** the empty-state card MUST NOT hide or replace that status

#### Scenario: Captain has an eligible team

- **WHEN** at least one captained team meets the tournament's required member count
- **THEN** the existing team-registration stepper MUST remain available with its current eligibility
  checks and actions
- **AND** the empty-state card MUST NOT be shown

#### Scenario: Captain has only undersized teams

- **WHEN** the captain has one or more teams but none meet the tournament's required member count
- **AND** any outstanding team invitations have not been accepted
- **THEN** the popup MUST retain the required-member-count explanation
- **AND** pending invitations MUST NOT count toward the required current team member count
- **AND** invited players MUST count toward the minimum only after accepting the invitation
- **AND** it MUST provide a separate `Manage teams` action
- **AND** it MUST NOT describe the captain as having no team

#### Scenario: Captain creates or cancels a team

- **WHEN** the create dialog is opened from the registration empty state
- **THEN** creating a team MUST leave the user on normal team management where inviting players is
  an explicit next action
- **AND** cancelling MUST leave team and tournament-registration state unchanged
- **AND** neither outcome MUST carry tournament context, reopen registration, select the new team,
  automatically register it, or return to the tournament
