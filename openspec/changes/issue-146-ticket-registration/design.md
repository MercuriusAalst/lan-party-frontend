## Context

The Info page is public and interactive-server rendered. The existing `IUserClient.GetCurrentUserProfileAsync` API provides the authenticated user's application profile; the user has approved adding available profile fields to rendered ticket links.

## Decisions

- Keep the ticket-only configured URL as the card's rendered `href` and fallback destination for anonymous visitors and when profile loading fails.
- During Info page initialization, use the existing server-side `IUserClient.GetCurrentUserProfileAsync` API to load the authenticated visitor's profile. Add mapped values to each ticket's native `_blank` link before rendering. No JavaScript, frontend registration endpoint, or proof is needed.
- Bound profile loading to five seconds. Do not cache or log profile values or generated URLs. A fresh page render after normal logout omits profile values; an already-open Info page keeps its loaded link until it is re-rendered.
- Build query strings with the framework query-string encoder. Accept only numeric configured entry IDs, omit empty values and IDs that already appear in the ticket URL, and avoid duplicate mapping keys.
- Use an event-wide set of optional field IDs and per-ticket full registration URLs. Missing or invalid URLs disable the relevant link without preventing startup.

## Risks / Trade-offs

- Opening registration transfers available configured profile fields to Google and may place them in browser history.
- The generated ticket URL contains profile values in its rendered link and may remain available in an already-open Info page after another tab signs out. Normal logout navigation refreshes the page before the logged-out visitor can use it.
