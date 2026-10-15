## MODIFIED Requirements

### Requirement: Match score entry follows the configured series format

The match details UI MUST explain the required wins for the configured Best-of format and the valid losing-score range using wording specific to that format. Player score submissions and administrator resolutions MUST be entered by choosing the winning participant and then the number of games the losing participant won, limited to the whole numbers from 0 up to one less than the required wins. The UI MUST set the winner's score to the required wins automatically and MUST NOT offer a free-text score entry. For a Best of 1 match the UI MUST require only the winner choice, MUST treat the loser's score as 0 and MUST NOT show the losing-score step. The UI MUST NOT preselect a winner unless the viewer's own earlier report or the official result is being shown. Score submission and resolution MUST stay disabled until the selection is complete, and the UI MUST show a live summary of the resulting score in the displayed participant order. The UI MUST still check the resulting score against the configured format before invoking the backend, while the backend remains authoritative. When the match format is unknown or unsupported, the UI MUST NOT show score guidance, MUST NOT render any score-selection control, result summary, submit button or resolve button, and MUST NOT reference guidance from `aria-describedby`. The selection controls MUST be operable with the keyboard, MUST expose their checked state to assistive technology and MUST NOT rely on colour alone to show the selected option.

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

#### Scenario: Unsupported format explains why no score can be reported

- **WHEN** the match format is not a supported Best-of format and the viewer could otherwise submit or resolve a score
- **THEN** the UI MUST NOT render any winner or losing-score control, result summary, or submit or resolve button
- **AND** it SHOULD show a short localized explanation that the score cannot be reported for this format and that an administrator should be contacted

## ADDED Requirements

### Requirement: Match dialog action sections are centred

The player and administrator action sections of the match details dialog MUST centre their titles, score selection groups and option rows, result summary, guidance text and action buttons within the full-width section. Action buttons MUST stay sized to their content and MUST wrap onto further lines when the available width is too small.

#### Scenario: Action sections are centred

- **WHEN** the match details dialog shows the "Your player actions" section or the administrator actions section
- **THEN** their content MUST be horizontally centred at desktop and at narrow viewport widths
