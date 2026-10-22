# Design: Home hero artwork and image delivery

## Current behavior

The Home page inserts featured tournament `<img>` elements after its first interactive render starts the public featured-tournament request. Browser evidence shows image caching is already effective: a cold load transfers bytes, a direct image tab in the same persistent profile is a disk-cache hit, and later homepage reloads are memory-cache hits. The delay is late URL discovery, not image retransfer.

## Decisions

- Remove only the tournament artwork blur and its now-unused wrapper. Preserve image order, sizing, opacity, rotation and drift. Keep glow/fade/gradient layers and card backdrop effects.
- Do not alter API or CDN cache headers and do not add a JavaScript/service-worker cache.
- Test whether prerendering the featured data can make the browser discover the image URLs earlier. If selected, use Blazor persistent component state so the interactive handoff consumes the prerender result instead of issuing a duplicate read. Sponsors remain independently loaded. Keep generation checks around later reload/save operations and preserve error, retry, disposal and confirmed-admin-order behavior.
- Accept a lifecycle change only if a matched browser measurement shows earlier hero image insertion without materially increasing initial document response time, duplicating the featured API request, or delaying sponsor rendering. Otherwise retain the current nonblocking load lifecycle and report the measured URL-discovery delay.

## Validation

Use a fresh persistent Chromium profile with normal cache enabled to compare cold and warm homepage loads, same-context direct image navigation, and in-app route/back behavior. Record image URLs, freshness/validator headers, CDP cache source and encoded bytes, first image insertion/load timing, document timing, and request counts. Capture the no-blur page at the required widths and reduced-motion mode.
