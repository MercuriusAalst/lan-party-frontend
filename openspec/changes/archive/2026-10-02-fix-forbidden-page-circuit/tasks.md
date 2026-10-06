## 1. Forbidden page layout

- [x] 1.1 Remove the redundant nested `LayoutView` from the authenticated `NotAuthorized` branch so the forbidden content renders in the framework default layout only.
- [x] 1.2 Keep HTTP 403, the forbidden copy, safe navigation actions, and the unrendered protected content.

## 2. Validation

- [x] 2.1 Capture the before-state fatal circuit exception from a protected direct request by temporarily restoring the nested layout wrapper.
- [x] 2.2 Prove the forbidden page stays interactive (theme toggle plus in-circuit navigation) with no fatal render console message.
- [x] 2.3 Strengthen the existing forbidden (sponsor and user profile) and not-found tests, and run the focused and class-wide authentication/sponsor suites against the real frontend and API.
