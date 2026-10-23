# Tasks

- [x] Add an optional, backwards-compatible `DeletionPending` field to the user action response.
- [x] Redirect every successful current-user deletion through safe logout with one neutral post-logout status target.
- [x] Reuse the existing status toast to show the localized deleted-account message and remove only the handled query.
- [x] Show the same confirmation for pending, completed, and legacy deletion responses without exposing cleanup state.
- [x] Keep mock deletion immediate and explicitly non-pending.
- [x] Add focused contract tests for pending, legacy, neutral toast text, query cleanup, safe navigation, and mock behavior.
- [x] Add the deterministic test-only cleanup key and stable exact-subject lifecycle to the local OIDC provider stub.
- [x] Update E2E deletion and stale-token replay coverage; final backend-integrated compilation belongs to architect verification.
- [x] Sync this checklist, validate, and archive the completed change.
