## 1. Contract and artifacts

- [ ] 1.1 Record the direct-score contract in OpenSpec, removing the confirm-ended command, capability and UI prerequisite.

## 2. Direct score entry

- [ ] 2.1 Remove the confirm-ended client, service, interface and mock methods, the `CanConfirmEnded` capability and the ended-confirmation action and status markup.
- [ ] 2.2 Submit a score directly, showing the awaiting-report state as ready to submit with no redundant confirmation after submitting.
- [ ] 2.3 Mirror the backend's atomic score-as-ended transition in the mock, preserving deadlines, duplicate, correction, BYE, terminal and authorization guards.
- [ ] 2.4 Keep the en-US and nl-BE lifecycle copy accurate for the remaining states.

## 3. Tests and validation

- [ ] 3.1 Update the contract and E2E callers for the removed route and capability and for direct score submission.
- [ ] 3.2 Run the focused match contract tests and compile the E2E project.
- [ ] 3.3 Validate strictly and archive the change.
