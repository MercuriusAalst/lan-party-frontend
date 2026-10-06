## 1. Status pages

- [x] 1.1 Render branded forbidden and not-found pages for direct SSR requests while preserving HTTP 403 and 404.
- [x] 1.2 Keep route authorization enforced and verify forbidden content does not expose the protected page.

## 2. Accessibility and invite dialog

- [x] 2.1 Render all boolean `aria-selected` and `aria-pressed` values as explicit strings matching the current control state.
- [x] 2.2 Keep invite dialog actions usable with the empty search dropdown open; cancellation closes the dialog without sending an invite.

## 3. Validation

- [x] 3.1 Strictly validate OpenSpec before and after implementation.
- [x] 3.2 Run the focused SSR, ARIA, and invite-dialog browser tests and the frontend build.

## 4. Authentication callback recovery

- [x] 4.1 Return cancellation and provider-failure feedback to a safe public destination when the challenge started at a protected route.
- [x] 4.2 Preserve valid public return destinations and successful sign-in return behavior.
- [x] 4.3 Cover cancellation from the admin-only user profile route in the browser and shared return-URL helper tests.

## 5. Status-page retry recovery

- [x] 5.1 Make same-route retry actions rerun the initial load while preserving ordinary route navigation for other status-page actions.
- [x] 5.2 Verify Profile and Manage Teams recover after their failed reads become available again.
