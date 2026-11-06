# Add theme-aware sponsor logo uploads and display

## Why

Sponsor artwork is often supplied for either a light or dark background. The
current single logo can disappear when the active site theme changes, and
administrators cannot provide the contrasting variant or remove it later.

## What Changes

- Extend the frontend sponsor contract with the optional `lightLogoUrl` response
  projection while retaining `logoUrl` as the required default logo.
- Extend sponsor create and update multipart requests. Create requires the
  default logo and accepts an optional light-coloured logo. Update can
  independently retain, replace, or explicitly remove either variant.
- Add localized administrator guidance and light/dark previews with recoverable
  validation and upload behavior.
- Render sponsor artwork through shared theme-aware behavior that follows the
  active application theme, updates immediately, and falls back to the default
  logo when no light variant exists.
- Apply the same behavior to every sponsor placement, including the homepage,
  sponsors page, and tournament sponsor projections.
- Keep legacy responses that omit `lightLogoUrl` compatible with the existing
  default-only behavior.

## Impact

Changes sponsor DTOs, models, API client/service and mock contracts, sponsor
management, shared sponsor rendering, public sponsor placements, localization,
and focused tests. The backend contract must be deployed before this frontend.
