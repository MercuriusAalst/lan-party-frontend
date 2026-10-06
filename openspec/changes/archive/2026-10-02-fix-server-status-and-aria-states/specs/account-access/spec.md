## ADDED Requirements

### Requirement: Authentication failures do not retry protected destinations

Identity-provider cancellation or failure MUST return to a safe public destination when the challenge began from a protected route. It MUST preserve the existing login status feedback and MUST NOT start another authentication challenge without a new user action. Successful authentication MUST retain its existing validated local return destination.

#### Scenario: Visitor cancels sign-in for a protected route

- **WHEN** an anonymous visitor cancels an identity-provider challenge that was started by a protected route
- **THEN** the application MUST return to a safe public page with cancellation feedback
- **AND** the protected route MUST NOT immediately start another identity-provider challenge
- **AND** the visitor MUST remain anonymous

#### Scenario: Visitor cancels sign-in for an admin-only user profile

- **WHEN** an anonymous visitor requests an admin-only user profile and cancels the identity-provider challenge
- **THEN** the application MUST return to a safe public page with cancellation feedback
- **AND** it MUST NOT immediately challenge the visitor again for that profile
- **AND** the visitor MUST remain anonymous

#### Scenario: Identity-provider failure follows a public destination

- **WHEN** the identity provider reports a failure for a valid public return destination
- **THEN** the application MUST return to that public destination with failure feedback

#### Scenario: Successful sign-in follows a protected destination

- **WHEN** a visitor completes sign-in for a validated protected return destination
- **THEN** the application MUST preserve the existing successful return-to-destination behavior
