## ADDED Requirements

### Requirement: Successful account deletion is confirmed after logout

When authenticated account deletion succeeds, the application MUST end the local authenticated session, return to a safe public destination, and show the localized neutral message "Your account has been deleted." regardless of any cleanup state supplied by the backend. The message MUST NOT mention a provider, queue, pending state, or LAN profile. After showing the message, the application MUST remove the handled deletion status from the address.

#### Scenario: Account deletion succeeds with pending cleanup

- **WHEN** the current-user deletion response indicates pending provider cleanup
- **THEN** the application MUST end the authenticated session through the existing logout flow
- **THEN** it MUST return to a safe public destination with the deleted-account status
- **THEN** it MUST show "Your account has been deleted." after logout
- **THEN** it MUST remove the handled status query without disturbing unrelated query parameters

#### Scenario: Account deletion succeeds with completed or legacy cleanup

- **WHEN** the current-user deletion response reports completed cleanup or omits the optional cleanup state
- **THEN** the application MUST end the authenticated session through the existing logout flow
- **THEN** it MUST return to a safe public destination with the deleted-account status
- **THEN** it MUST show the same "Your account has been deleted." message
- **THEN** it MUST remove the handled status query without disturbing unrelated query parameters

#### Scenario: Post-logout return target is unsafe

- **WHEN** a deletion return target is not a safe public local destination
- **THEN** the existing logout protection MUST redirect to the public home page
- **AND** no protected destination or external URL MUST be replayed
