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
- **AND** it MUST state that the other side then has five minutes to agree or report a correction, otherwise the reported score becomes final

#### Scenario: Score-confirmation description is perspective-neutral

- **WHEN** the backend reports a score under confirmation
- **THEN** the description MUST be correct for both the reporting side and the other side
- **AND** it MUST state that the side that has not reported yet has five minutes to agree or report a correction, otherwise the reported score becomes final
