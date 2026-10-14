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
