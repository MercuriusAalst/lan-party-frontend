## MODIFIED Requirements

### Requirement: Match score entry follows the configured series format

The match details UI MUST explain the required wins for the configured Best-of format and the valid losing-score range using wording specific to that format. Player score submissions and administrator resolutions MUST accept only integer scores where one participant reaches the required wins and the other has a lower, nonnegative score. Score text that is not a nonnegative whole number, such as a decimal, a negative or signed number, or an overflowing value, MUST be treated as an entered but invalid score and MUST NOT fall back to a previously entered value. The UI MUST block incomplete or invalid scores before invoking the backend, while the backend remains authoritative. When the match format is unknown, the UI MUST NOT show score guidance and MUST NOT reference it from `aria-describedby`.

#### Scenario: Player enters a decisive score

- **WHEN** a player enters a score with one participant at the required wins and the other below it
- **THEN** the UI MUST allow submission
- **AND** it MUST submit the participant scores in their displayed order

#### Scenario: Player enters an incomplete or invalid score

- **WHEN** either score is missing, negative, tied, exceeds the required wins, or neither participant reaches the required wins
- **THEN** the UI MUST disable score submission
- **AND** it MUST show accessible guidance explaining the valid score for the configured format
- **AND** the submission handler MUST NOT invoke the backend

#### Scenario: Player enters a non-integer score

- **WHEN** a player enters score text that is not a nonnegative whole number, such as `2.5`
- **THEN** the UI MUST treat that score as entered and invalid
- **AND** it MUST disable score submission and MUST NOT submit any previously entered value

#### Scenario: Administrator enters an incomplete or invalid resolution score

- **WHEN** an administrator score resolution has missing or invalid series scores
- **THEN** the UI MUST disable resolution
- **AND** the resolution handler MUST NOT invoke the backend

#### Scenario: Format-specific guidance

- **WHEN** the match format is Best of 1, Best of 3 or Best of 5
- **THEN** the UI MUST show guidance worded for that format, such as "Best of 1: the winner has 1 win, the other side 0."

#### Scenario: Unknown match format

- **WHEN** the match format is not a supported Best-of format
- **THEN** the UI MUST NOT render score guidance
- **AND** the score inputs MUST NOT reference a missing guidance element through `aria-describedby`
