# Finish the Auth0 account lifecycle in the front-end

## Why

Account deletion removes the local account and may leave provider cleanup to finish independently. The front-end must end the authenticated session and confirm successful deletion with one neutral message regardless of cleanup state. Older backends can still omit the optional cleanup flag, so the front-end must keep working while the response model is deployed independently.

The Playwright provider stub must also model account cleanup closely enough for the backend cleanup worker: provider creation time must remain stable for one provider persona incarnation, and deleting a subject must remove that exact provider record so recreation starts with a fresh timestamp and clean roles and identities.

## What Changes

- The current-user deletion response adds an optional `DeletionPending` flag that defaults to false.
- Every successful deletion response logs the user out through the existing safe return URL and returns to the public home page with one deleted-account status query.
- The existing post-redirect status component shows "Your account has been deleted." after the session is signed out, removes the handled query, and preserves login and registration status handling.
- The optional cleanup state remains wire-compatible but does not change the user-facing confirmation.
- Mocks keep immediate local deletion semantics and report that deletion is not pending.
- Contract tests cover pending and legacy responses, message rendering, query cleanup, safe logout navigation, and mock behavior.
- The E2E provider fixture supplies a deterministic test-only 32-byte cleanup encryption key and models stable provider creation time plus exact-subject deletion and clean recreation.

## Impact

Updates the `account-access` and `live-backend-contract-integration` specifications. The current-user deletion response change is additive. Admin delete client methods remain unchanged because no front-end admin deletion UI consumes their response. No new page, dependency, or authentication flow is introduced.
