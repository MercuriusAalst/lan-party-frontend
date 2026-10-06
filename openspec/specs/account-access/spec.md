# account-access Specification

## Purpose
TBD - created by archiving change issue-40-separate-login-registration-flows. Update Purpose after archive.
## Requirements
### Requirement: Signed-out navigation distinguishes account actions

The signed-out header navigation MUST expose separate Login and Register actions
on mobile and desktop layouts.

#### Scenario: Existing user chooses login

- **WHEN** an anonymous visitor selects Login
- **THEN** the application MUST start the existing-user authentication challenge
- **AND** the login entry point MUST remain focused on signing in rather than
  collecting profile-registration details

#### Scenario: New user chooses registration

- **WHEN** an anonymous visitor selects Register
- **THEN** the application MUST start the registration authentication challenge
- **AND** the challenge MUST request account creation semantics from the identity
  provider
- **AND** the current safe local return destination MUST be preserved

### Requirement: Registration reuses authenticated profile onboarding

After a successful registration challenge, the application MUST reuse the
existing authenticated profile-completion contract to collect profile data.

#### Scenario: Registration challenge completes

- **WHEN** the identity provider returns an authenticated new user
- **THEN** the application MUST present guided profile onboarding when the
  profile is incomplete
- **AND** required and optional profile fields MUST be distinguishable
- **AND** field validation errors MUST be shown inline where the existing form
  validation supports them

#### Scenario: Registration returns an already-complete profile

- **WHEN** the authenticated registration flow loads a profile that is already
  complete
- **THEN** the application MUST redirect to the validated local return
  destination
- **AND** it MUST NOT display registration completion before a successful
  profile completion request

#### Scenario: Profile onboarding completes

- **WHEN** the user submits valid required profile data and the profile request
  succeeds
- **THEN** the application MUST show a clear completion or authenticated state
- **AND** the user MUST be returned to the validated local destination

### Requirement: Registration navigation is recoverable

The registration flow MUST provide clean back and cancellation behavior without
discarding a safe return destination or leaving a stale onboarding state.

#### Scenario: User cancels registration

- **WHEN** a user cancels before completing profile onboarding
- **THEN** the application MUST return to a safe local destination or home
- **AND** it MUST NOT submit incomplete profile data

#### Scenario: User goes back from onboarding

- **WHEN** a user chooses Back during registration onboarding
- **THEN** the application MUST return to the previous registration step or
  entry destination without losing already entered values unnecessarily

#### Scenario: Registration or profile request fails

- **WHEN** the identity-provider challenge or profile request fails
- **THEN** the application MUST show a recoverable, understandable error state
- **AND** it MUST NOT treat the user as authenticated solely because a client
  navigation action was attempted

### Requirement: Account redirects are safe and parity-preserving

Login and registration redirects MUST accept only local application destinations
and MUST preserve mock-login behavior when mock backend mode is enabled.

#### Scenario: Unsafe return URL is supplied

- **WHEN** login or registration receives an absolute, protocol-relative,
  backslash-prefixed, or control-character-containing return URL
- **THEN** the application MUST fall back to home
- **AND** it MUST NOT redirect to an external destination

#### Scenario: Mock mode is enabled

- **WHEN** an anonymous visitor uses login or registration while mock backend
  mode is enabled
- **THEN** the existing mock authentication path MUST remain usable
- **AND** the resulting authenticated navigation MUST retain its current
  behavior

### Requirement: Authenticated navigation remains stable

Authenticated users MUST continue to receive the existing profile, team,
notification, admin, logout, and ticket navigation without a registration action
being shown as an alternative authenticated state.

#### Scenario: Authenticated visitor views the header

- **WHEN** a visitor is authenticated
- **THEN** the header MUST show the existing authenticated navigation
- **AND** signed-out Login and Register actions MUST NOT be shown

### Requirement: Logout preserves safe public context

The application MUST capture the current relative URL before an authenticated
user logs out and MUST return to that URL after logout when it is a public,
local application destination.

#### Scenario: User logs out from a public page

- **WHEN** an authenticated user logs out from a public page with a path,
  query, or fragment
- **THEN** the logout request MUST carry the current relative URL as its
  return target
- **AND** the user MUST be returned to the same public path, query, and
  fragment after the session is cleared

### Requirement: Logout never replays protected destinations

The application MUST treat authenticated-only routes as unsafe logout return
targets and MUST use a safe public fallback such as `/` after logout.

#### Scenario: User logs out from a protected page

- **WHEN** an authenticated user logs out from a protected route such as
  `/profile`, `/complete-profile`, `/teams/manage`, or `/admin/sponsors`
- **THEN** the logout flow MUST return to a safe public location
- **AND** it MUST NOT retain or replay the protected route, query, or fragment

### Requirement: Logout return targets resist URL manipulation

The logout return-target policy MUST reject external, absolute,
protocol-relative, backslash-prefixed, and control-character-containing
targets, including when those values are supplied through query-string
encoding. It MUST also reject ambiguous local paths containing dot segments,
encoded dot segments, internal or encoded backslashes, encoded separators,
encoded control characters, malformed escapes, or nested percent encoding.
Protected descendant paths and `/account/logout` itself MUST be treated as
unsafe.

#### Scenario: Attacker supplies a malicious logout target

- **WHEN** a logout request contains an unsafe return target
- **THEN** the application MUST use the safe public fallback
- **AND** it MUST NOT redirect the browser outside the application

#### Scenario: Ambiguous target attempts canonicalization

- **WHEN** a logout request contains a local target that can resolve through
  dot-segment, backslash, encoded-separator, control-character, malformed
  escape, or repeated-decoding behavior
- **THEN** the application MUST use the safe public fallback
- **AND** it MUST NOT normalize the target before deciding whether it is safe

#### Scenario: Public target contains query and fragment data

- **WHEN** a logout request contains a valid local public target with query and
  fragment components
- **THEN** validation MUST preserve those components exactly
- **AND** validation MUST evaluate the route path separately from its query
  and fragment

### Requirement: Existing account redirects remain compatible

The logout redirect change MUST NOT alter the existing login, registration,
mock-login, Auth0 challenge, or Auth0 callback redirect behavior.

#### Scenario: User authenticates through login or registration

- **WHEN** login or registration completes or returns through an Auth0 callback
- **THEN** its existing validated local return destination behavior MUST remain
  unchanged
- **AND** the logout return-target policy MUST NOT be used to discard a valid
  account onboarding or callback destination

### Requirement: Live provider logout uses a fixed callback and encoded return target

The live Auth0 logout flow MUST send the provider to the fixed local
`/account/logout/callback` URI with the already server-validated logout target
as an encoded `returnUrl` query parameter. It MUST NOT put the dynamic target
in the callback path. The application MUST clear its local authentication
cookie before starting the Auth0 sign-out operation so a remote provider
failure cannot leave the local session authenticated.

#### Scenario: Auth0 logout returns through the fixed callback

- **WHEN** an authenticated user logs out from a public page in live mode
- **THEN** the provider logout request MUST use the fixed
  `/account/logout/callback` callback with an encoded `returnUrl` query
  parameter
- **AND** the validated public path, query, and fragment MUST survive the
  provider round trip
- **AND** the user's authenticated session MUST be cleared before the provider
  round trip completes

#### Scenario: Callback revalidates a valid return target

- **WHEN** the provider returns to `/account/logout/callback` with a valid
  `returnUrl` query parameter
- **THEN** the callback MUST revalidate the decoded target through the logout
  return-target policy
- **AND** it MUST issue a local redirect to the resulting safe target

#### Scenario: Callback receives missing, unsafe, or malformed return target

- **WHEN** the provider returns without a usable `returnUrl` query parameter
- **THEN** the callback MUST revalidate the missing, unsafe, or malformed value
  through the logout return-target policy
- **AND** it MUST redirect locally to `/`

#### Scenario: Remote provider sign-out fails

- **WHEN** the Auth0 sign-out operation fails after logout has started
- **THEN** the local authentication cookie MUST already be cleared
- **AND** the logout response MUST complete with a redirect to the validated public return target
- **AND** a later request to a protected route MUST be treated as anonymous

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

