# Design: Home hero artwork and image delivery

## Current behavior

The browser cache is working. Cold requests for the stable hero image URLs transfer bytes once; a same-context direct image visit and warm homepage reload are served from browser cache. The delay came from Home discovering the URLs after interactive startup, not from downloading them again.

## Decisions

- Remove only the tournament artwork blur and its unused wrapper. Preserve image order, sizing, opacity, rotation and drift. Keep glow/fade/gradient layers and card backdrop effects.
- Load the public featured-tournament result during streamed prerender and persist it through Blazor's prerender-to-interactive handoff. This makes image URLs available earlier without issuing the initial featured request twice.
- Start public sponsor loading independently from the featured request. Reuse completed sponsor state during handoff; if the sponsor request is still pending when prerender state is serialized, the interactive component can issue its own request.
- Keep ordinary browser HTTP caching as the only image cache. Do not add a JavaScript/service-worker cache or change the backend/CDN cache headers.
- Keep the existing shared load path, generation checks, error/retry handling, confirmed admin ordering and disposal protections.

## Validation

A headed Chromium run with normal persistent-profile caching measured cold and warm homepage loads, same-context direct image navigation, cache response headers/bytes, first image insertion/load, and document timing. Final screenshots and computed filter/animation/overflow checks cover 390, 768, 900, and 1440 CSS-pixel widths plus reduced motion. See `docs/performance/issue-120/profile-results.md` for results, exact commands, and hardware limits.
