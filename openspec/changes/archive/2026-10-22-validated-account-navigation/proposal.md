# Show account identity only after LAN profile validation

## Why

The authenticated navigation currently falls back to Auth0 claims while the LAN profile request is pending or unavailable. That can briefly expose a provider nickname, name, email, or subject, including immediately after authentication and before deleted-account protection signs the user out.

## What Changes

- The account header shows the localized neutral account label and a generic avatar until a completed LAN profile has been validated.
- A validated completed profile supplies the displayed username and profile picture.
- Missing, incomplete, deleted, failed, or superseded profile responses keep the header neutral.
- Profile refresh notifications apply only to the identity and request that initiated them, and invalid responses clear previously validated navigation data.
- Existing onboarding, deleted-user sign-out, roles, notifications, realtime updates, and current-user profile refresh behavior remain intact.

## Impact

Updates the `account-access` and `site-navigation` specifications. The existing current-user profile API contract remains unchanged. No backend, dependency, or broader registration-flow change is included.
