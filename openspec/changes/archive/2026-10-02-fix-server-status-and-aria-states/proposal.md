## Why

Static server rendering returns an empty document for unknown routes and authenticated requests that fail route authorization, even though the interactive router already defines branded recovery pages. Several boolean ARIA attributes also render as empty strings, so assistive technology cannot reliably determine selected or pressed state. The invite search empty state can cover its dialog actions.

When sign-in is cancelled after a protected route starts the identity-provider challenge, the current failure callback returns to that protected route and immediately starts the challenge again.

The Profile and Manage Teams load-error pages also link back to their current route for retry, but Blazor's same-route client navigation does not rerun the page's initial load.

## What Changes

- Render the existing forbidden and not-found recovery copy for direct HTTP requests while preserving the 403 and 404 response codes.
- Serialize boolean ARIA state as explicit `true` or `false` values.
- Keep invite dialog actions reachable while the empty search state is visible.
- Return identity-provider cancellation and failure to a safe public destination when the requested return route requires authentication.
- Make same-route status-page retries rerun the page's initial load.

## Capabilities

### Modified Capabilities

- `responsive-accessible-states`: Direct server requests render status content, and boolean accessibility states remain explicit.
- `user-owned-team-management`: Invite dialog actions remain reachable while search results are empty.
- `account-access`: Authentication failure callbacks do not retry a protected route and preserve successful local return behavior.
- `api-unavailable-resilience`: Same-route retry actions rerun the failed initial page load.

## Impact

- Blazor server request pipeline and status-page routing.
- Cookie authorization-denied response handling.
- Existing Razor controls that expose selected or pressed state.
- Invite dialog layout and its focused UI tests.
- No API, database, or package changes.
