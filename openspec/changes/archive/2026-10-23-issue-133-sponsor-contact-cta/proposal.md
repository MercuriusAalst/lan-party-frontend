# Route Sponsor Inquiries to the Contact Form

## Why

Both sponsor inquiry calls to action currently target a fragment that does not open the existing contact form. Visitors can be left on the sponsors page instead of reaching a usable inquiry destination.

## What Changes

- The sponsor inquiry call to action shown alongside populated sponsor content MUST navigate to `/info#contact-info`.
- The sponsor inquiry call to action in the empty sponsor state MUST use the same destination.
- Browser tests MUST verify that the existing contact form is in the viewport after navigation on desktop and mobile.
- No new form, API endpoint, backend field, dependency, or sponsor data change is required.

## Impact

Updates the `public-sponsor-showcase` specification and the public sponsors page navigation. The existing Info page contact form and its submission behavior remain unchanged.
