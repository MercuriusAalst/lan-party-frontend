## MODIFIED Requirements

### Requirement: Team registration uses a three-step MudBlazor Stepper

An authenticated captain MUST use a MudBlazor Stepper with exactly three logical steps: team selection, roster selection, and review/submit.

#### Scenario: Captain selects a team

- **WHEN** a captain opens team registration
- **THEN** Step 1 MUST list only teams the current user captains that have at least the tournament's required team size in current members
- **AND** teams below the required size MUST be hidden, including teams with an existing registration
- **AND** any restored or automatically selected team MUST satisfy the same captain and member-count criteria
- **AND** the popup MUST explain that undersized teams are hidden
- **AND** the selected team MUST be checked against backend team eligibility before the captain can continue
- **AND** the team selection step MUST remain active until a valid team is selected

#### Scenario: Captain selects a roster

- **WHEN** the captain reaches Step 2
- **THEN** the step title MUST identify the selected team as `Roster members for {team}` and show selected versus required roster count inline
- **AND** the separate selected-team block and roster-size label/help MUST NOT be shown
- **AND** the current captain MUST remain selected automatically and MUST NOT appear as a roster candidate
- **AND** ineligible roster candidates MUST retain a meaningful disabled state and error handling
- **AND** standalone eligible/unavailable status messages MUST NOT occupy the roster status area
- **AND** the captain MUST NOT progress to review until local roster constraints pass
- **AND** when navigating backward and forward, the current draft MUST be preserved and revalidated

#### Scenario: Captain advances out of the roster step

- **WHEN** the captain advances from Step 2 through the primary action or a later step header and the draft is revalidated against backend roster eligibility
- **THEN** the revalidation MUST keep the current roster, eligibility, and action state rendered until the backend response arrives
- **AND** the focused action MUST NOT be replaced or disabled while the revalidation is in flight
- **AND** the captain MUST advance only when the revalidation confirms the current selection
- **AND** a rejected revalidation MUST show one concise inline reason above the steps and MUST NOT advance the step

#### Scenario: Captain reviews and submits

- **WHEN** the captain reaches Step 3
- **THEN** the step title MUST be `Summary` and show selected versus required roster count inline
- **AND** the separate required-size section MUST NOT be shown
- **AND** roster member cards MUST visually distinguish confirmed and pending members using each member's actual confirmation state
- **AND** a captain member card MUST show its localized captain badge after the member name
- **AND** submission MUST send the exact selected roster through the existing backend team roster route
- **AND** cancellation of an existing registration MUST appear beside the save action when Summary is reachable
- **AND** a pending response MUST remain visible as pending until all required confirmations complete

#### Scenario: Captain cancels without reaching the summary

- **WHEN** a captain-managed registration exists and filtering or invalid/unavailable roster eligibility prevents the captain from reaching Summary
- **THEN** cancellation MUST remain available before Summary, including when no team option remains
- **AND** confirmation and deletion MUST target the current captain-managed registration independently of the filtered selection
- **AND** cancelling MUST NOT change another selected team or roster draft

#### Scenario: Captain navigates between steps

- **WHEN** the captain activates a step header to navigate through the Stepper
- **THEN** the step headers MUST be keyboard navigable and expose visible focus and active states
- **AND** a clickable step header MUST show a soft rounded hover surface
- **AND** navigation to a later step MUST be blocked unless all prior team and roster requirements pass
- **AND** navigation to an earlier step MUST remain available while a request is not submitting
- **AND** the existing forward and back action buttons MUST NOT be duplicated outside the Stepper

### Requirement: Tournament registration is an accessible popup workflow

The tournament detail page MUST expose a concise registration trigger and render the registration workflow in an accessible modal popup rather than inline in the page content.

#### Scenario: Visitor opens registration

- **WHEN** a visitor selects the tournament registration action
- **THEN** the workflow MUST open in a labelled dialog with a clear borderless close control
- **AND** the dialog MUST retain an accessible name when the visible `Registration` eyebrow is removed
- **AND** the first step MUST NOT show the redundant sentence `Choose a team you captain to register or update.`
- **AND** keyboard focus MUST move into the dialog, remain usable within it, and return to the trigger after dismissal
- **AND** the underlying tournament page MUST not render a second inline registration component

#### Scenario: Captain uses registration actions

- **WHEN** a captain reaches a registration step where an action is disabled
- **THEN** the primary action MUST retain a clearly disabled visual style
- **AND** the popup MUST NOT render separate `Change team` or `Back` buttons when the stepper provides navigable steps
- **AND** summary and roster content MUST preserve accessible labels and meaningful disabled states

#### Scenario: Captain reads registration options

- **WHEN** a captain views the team, roster, or summary options in the popup
- **THEN** team, roster, and summary cards MUST keep their natural width when only a few options are shown instead of stretching across the whole dialog
- **AND** every registration cancellation action MUST render with the shared danger treatment
- **AND** the dialog MUST remain readable without page-wide horizontal overflow at narrow widths