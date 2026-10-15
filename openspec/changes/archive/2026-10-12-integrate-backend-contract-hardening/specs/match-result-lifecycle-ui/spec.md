## ADDED Requirements

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
