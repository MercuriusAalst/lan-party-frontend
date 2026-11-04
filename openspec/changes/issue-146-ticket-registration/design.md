## Context

The Info page is public and interactive-server rendered. Profile details must not be added to rendered links or shared state. The existing `IUserClient.GetCurrentUserProfileAsync` endpoint provides the authenticated user's application profile.

## Decisions

- Keep the ticket-only configured URL as the card's rendered `href` and fallback destination.
- On click, a small browser-side handler opens a blank tab synchronously, then asks the component for the current user's configured profile fields and navigates that tab. This avoids popup blocking while keeping personal values out of markup.
- Fetch authentication state and the current user's own profile for each activation. Do not cache values. If anonymous, misconfigured, or profile loading fails, use the ticket-only URL.
- Build query strings with the framework query-string encoder and append only configured field mappings with non-empty values.
- Use an event-wide set of optional field IDs and per-ticket full registration URLs. Missing or invalid URLs disable the relevant link without preventing startup.

## Risks / Trade-offs

- Opening registration transfers the configured profile fields to Google and may place them in browser history; localized text discloses this before activation.
- If a browser blocks a blank tab despite a direct user click, the normal ticket-only card link remains available as fallback.
