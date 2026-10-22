# Tasks

- [x] Add an optional, backwards-compatible `DeletionPending` field to the user action response.
- [x] Redirect pending current-user deletion through safe logout with a dedicated post-logout status target.
- [x] Reuse the existing status toast to show the localized pending-cleanup message and remove only the handled query.
- [x] Preserve completed and legacy deletion logout behavior without claiming queued provider cleanup.
- [x] Keep mock deletion immediate and explicitly non-pending.
- [x] Add focused contract tests for pending, legacy, toast, query cleanup, safe navigation, and mock behavior.
- [x] Add the deterministic test-only cleanup key and stable exact-subject lifecycle to the local OIDC provider stub.
- [x] Update E2E deletion and stale-token replay coverage; final backend-integrated compilation belongs to architect verification.
- [x] Sync this checklist, validate, and archive the completed change.
