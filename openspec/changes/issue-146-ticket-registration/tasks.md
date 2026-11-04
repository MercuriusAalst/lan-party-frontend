## 1. Specification

- [x] 1.1 Update the Info page requirement for configured ticket links and profile prefill

## 2. Configuration and interaction

- [x] 2.1 Add optional registration URLs and configurable Google Forms field mappings
- [x] 2.2 Implement accessible ticket links and click-time current-profile prefill with ticket-only fallback
- [x] 2.3 Add localized disclosure and focus styling

## 3. Verification

- [x] 3.1 Add contract and public-site coverage for destinations, accessibility, encoding, mapping failures, current-user changes, and profile fallback
- [ ] 3.2 Validate OpenSpec and run targeted build and tests

Validation note: strict OpenSpec validation, the web build, the E2E project build, and 7 focused contract tests passed. The E2E runtime could not reach test execution: fixture startup stalled and VSTest timed out after 90 seconds.
