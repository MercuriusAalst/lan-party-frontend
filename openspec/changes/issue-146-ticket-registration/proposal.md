## Why

Visitors can read ticket details but cannot start registration from the ticket they chose. Signed-in visitors also have to re-enter profile details that the application already knows.

## What Changes

- Make each configured ticket card an accessible link to its matching prefilled registration form.
- Keep the general registration link and venue purchase guidance.
- For signed-in users, prefill available profile name and username at activation time, with clear disclosure that those values are sent to Google Forms.
- Keep ticket URLs and optional Google Forms field mappings in application configuration.

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- `lan-info-page`: ticket cards open configured registration forms, with optional current-user profile prefill and accessible privacy disclosure.

## Impact

- Info page component and styles, LAN event options and appsettings, English and Dutch localization, public-site E2E tests, and the `lan-info-page` OpenSpec requirement. No API contract or backend changes.
