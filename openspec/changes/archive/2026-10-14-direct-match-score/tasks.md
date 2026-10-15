## 1. Contract and artifacts

- [x] 1.1 Record the direct-score contract in OpenSpec, removing the confirm-ended command, capability and UI prerequisite.

## 2. Direct score entry

- [x] 2.1 Remove the confirm-ended client, service, interface and mock methods, the `CanConfirmEnded` capability and the ended-confirmation action and status markup.
- [x] 2.2 Submit a score directly, showing the awaiting-report state as ready to submit with no redundant confirmation after submitting.
- [x] 2.3 Mirror the backend's atomic score-as-ended transition in the mock, preserving deadlines, duplicate, correction, BYE, terminal and authorization guards.
- [x] 2.4 Keep the en-US and nl-BE lifecycle copy accurate for the remaining states.

## 3. Tests and validation

- [x] 3.1 Update the contract and E2E callers for the removed route and capability and for direct score submission.
- [x] 3.2 Run the focused match contract tests (56 passed) and build the E2E project against the backend PR #151 head (`00f58bd9`); the E2E build only fails when `MERCURIUS_BACKEND_ROOT` / `BackendProjectRoot` does not point at a backend checkout, and with it set the build succeeds (0 errors).
- [x] 3.3 Validate strictly and archive the change.
