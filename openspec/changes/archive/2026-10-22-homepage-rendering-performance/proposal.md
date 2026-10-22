# Change: Remove hero artwork blur and surface images earlier

## Why

The user asked for sharp tournament artwork and reported that homepage images appeared uncached and slow. A fresh persistent Chromium profile shows the three image URLs are stable and cacheable (`public, max-age=31536000` with weak ETags): cold homepage requests transfer the image bytes once, while a direct same-context visit and subsequent homepage reloads return from disk/memory cache with zero image bytes. The browser only discovers the hero URLs after the interactive Home component loads featured tournaments; on the measured cold page, DOM content loaded at 496 ms but hero image elements were inserted at 1,366 ms.

## What changes

- Remove the blur from the three Home tournament images while retaining their opacity, sizing, rotation and drift, plus the existing glow, fade, gradients and card backdrop treatment.
- Investigate whether initial prerender can expose the already-cached hero image URLs earlier without duplicating the featured API call, delaying sponsors, or weakening the existing generation/disposal/admin-save protections.
- Keep ordinary browser HTTP caching as the only image cache. Do not add an app cache, service worker, cache-busting URL, or backend/CDN configuration change.

## Scope

Frontend Home only, its focused lifecycle/visual evidence, and the visual-correction spec.
