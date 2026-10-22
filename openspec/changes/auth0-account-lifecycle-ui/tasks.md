# Tasks

- [ ] Add an optional, backwards-compatible `DeletionPending` field to the user action response.
- [ ] Redirect pending current-user deletion through safe logout with a dedicated post-logout status target.
- [ ] Reuse the existing status toast to show the localized pending-cleanup message and remove only the handled query.
- [ ] Preserve completed and legacy deletion logout behavior without claiming queued provider cleanup.
- [ ] Keep mock deletion immediate and explicitly non-pending.
- [ ] Add focused contract tests for pending, legacy, toast, query cleanup, safe navigation, and mock behavior.
- [ ] Add the deterministic test-only cleanup key and stable exact-subject lifecycle to the local OIDC provider stub.
- [ ] Update E2E deletion assertions for pending local removal and compile the E2E project when the backend revision is available.
- [ ] Sync this checklist, validate, and archive the completed change.
