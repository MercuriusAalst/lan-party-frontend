## ADDED Requirements

### Requirement: Account navigation has a stable neutral state

The authenticated account navigation MUST use the localized neutral account label and a generic avatar whenever the active LAN profile has not been validated. The account control MUST preserve its existing layout and dimensions while loading or unvalidated identity data is replaced by a completed LAN profile.

#### Scenario: Authenticated account is not yet validated

- **WHEN** the account navigation is rendered for an authenticated user without a validated completed LAN profile
- **THEN** the visible label MUST use the localized neutral account label
- **AND** the avatar MUST use the generic placeholder

#### Scenario: Validated profile replaces the neutral state

- **WHEN** a completed LAN profile becomes available
- **THEN** the navigation MUST replace the neutral label and avatar with the validated LAN username and picture
- **AND** the account control MUST retain its existing dimensions and accessible account-menu labeling
