## ADDED Requirements

### Requirement: Account deletion response remains additive and mock-compatible

The front-end user action response MUST accept an optional pending-provider-cleanup flag whose absence means cleanup is not pending. The flag MUST remain wire-compatible but MUST NOT change the neutral success confirmation shown after a successful current-user deletion. Mock deletion MUST keep its immediate local semantics and report that provider cleanup is not pending. Existing admin delete API client signatures MUST remain unchanged while no admin deletion UI consumes their response body.

#### Scenario: Legacy backend omits pending cleanup

- **WHEN** deletion returns a valid response without the optional pending-provider-cleanup field
- **THEN** the front-end MUST deserialize it as non-pending
- **AND** it MUST show the same neutral deleted-account confirmation used for a pending response

#### Scenario: Backend reports pending cleanup

- **WHEN** deletion returns a valid response with pending provider cleanup
- **THEN** the front-end MUST show the same neutral deleted-account confirmation used for a legacy response
- **AND** it MUST NOT expose the cleanup state in user-facing text

#### Scenario: Mock account deletion runs

- **WHEN** a user deletes an account through the mock backend
- **THEN** the mock MUST complete its local deletion behavior
- **AND** it MUST report that provider cleanup is not pending

#### Scenario: E2E provider cleanup is simulated

- **WHEN** the local OIDC provider creates a persona and later deletes that exact provider subject
- **THEN** it MUST expose a creation timestamp that remains stable for that provider incarnation
- **AND** deletion MUST remove only that exact provider record
- **WHEN** the same subject is recreated
- **THEN** the provider MUST expose a new creation timestamp
- **AND** the recreated persona MUST start without the deleted persona's roles or identities
