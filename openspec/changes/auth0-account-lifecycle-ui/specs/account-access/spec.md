## ADDED Requirements

### Requirement: Pending account cleanup is explained after logout

When authenticated account deletion reports that provider cleanup is pending, the application MUST end the local authenticated session, return to a safe public destination, and show a localized neutral message explaining that the LAN profile was removed and login cleanup was queued. The message MUST NOT claim that provider cleanup has completed. After showing the message, the application MUST remove the handled deletion status from the address.

#### Scenario: Account deletion reports pending cleanup

- **WHEN** the current-user deletion response indicates pending provider cleanup
- **THEN** the application MUST end the authenticated session through the existing logout flow
- **THEN** it MUST return to a safe public destination with the pending-deletion status
- **THEN** it MUST show the localized pending-cleanup message after logout
- **THEN** it MUST remove the handled status query without disturbing unrelated query parameters

#### Scenario: Account deletion reports completed or legacy cleanup

- **WHEN** the current-user deletion response reports completed cleanup or omits the optional cleanup state
- **THEN** the application MUST retain the existing logout behavior
- **AND** it MUST NOT show the pending-cleanup message

#### Scenario: Post-logout return target is unsafe

- **WHEN** a pending deletion return target is not a safe public local destination
- **THEN** the existing logout protection MUST redirect to the public home page
- **AND** no protected destination or external URL MUST be replayed
