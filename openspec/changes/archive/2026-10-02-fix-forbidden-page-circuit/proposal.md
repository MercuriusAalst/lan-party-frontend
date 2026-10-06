## Why

After the branded forbidden page was introduced for authenticated users, the
interactive Blazor circuit for a forbidden direct request tears down. The forbidden
branch wrapped its recovery content in its own `LayoutView` even though
`AuthorizeRouteView` already renders the `NotAuthorized` fragment inside `DefaultLayout`
for the route. Two `MainLayout` instances then render concurrently, so the second
`MudPopoverProvider` outlet registers a duplicate section ID
(`mud-overlay-to-popover-provider`). That throws
`InvalidOperationException: There is already a subscriber to the content with the given
section ID 'mud-overlay-to-popover-provider'`, which fails the circuit and disconnects
the user before the recovery page can be interacted with.

## What Changes

- Render the forbidden recovery content directly inside the framework-provided default
  layout instead of a second nested `LayoutView`.
- Keep the HTTP 403 response, the forbidden copy, and the safe navigation actions
  unchanged, and keep the protected route content unrendered.
- The forbidden page remains interactive after a direct protected-request denial.

## Capabilities

### Modified Capabilities

- `responsive-accessible-states`: The forbidden recovery page renders in a single
  application layout and keeps a live Blazor circuit so the shell stays interactive.

## Impact

- Blazor router and `AuthorizeRouteView` layout composition for the forbidden branch.
- No API, database, package, or authentication-policy changes.
