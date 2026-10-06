## ADDED Requirements

### Requirement: Mock administrators can return to the user persona

When mock backend mode is enabled, the navigation MUST provide an authenticated mock administrator with a direct way to switch to the user persona. The action MUST remain unavailable when mock backend mode is disabled.

#### Scenario: Mock administrator switches to user persona

- **WHEN** an administrator is signed in through enabled mock backend mode
- **THEN** the account menu MUST offer a user persona link
- **AND** selecting it MUST sign in with the user persona and navigate to a valid local destination

#### Scenario: Live administrator uses navigation

- **WHEN** an administrator uses the live backend
- **THEN** the mock user persona link MUST NOT be available
