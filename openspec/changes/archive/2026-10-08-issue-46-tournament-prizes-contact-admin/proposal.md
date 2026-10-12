## Why

Tournament setup cannot currently record who the contact administrator is or what the top-three
prizes are. Players therefore have no public contact point for a tournament and no visibility of
what is being played for, before or after results are known.

## What Changes

- Admin tournament create/edit forms gain a contact-administrator picker (selectable and clearable)
  and three optional prize fields for 1st, 2nd, and 3rd place.
- The front end loads selectable administrators from the admin-only administrator list resource and
  sends the selected contact plus prize values through the existing multipart tournament
  create/update contract.
- The public tournament detail presents the configured contact administrator and the configured
  prizes, showing each prize under its matching top-three placement once placements exist and on its
  own before placements are known.
- Missing contact and empty prize values are omitted without placeholder noise.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `live-backend-contract-integration`: tournament create/update multipart carries the contact
  administrator and prize fields, and the front end consumes the admin-only administrator list
  resource.
- `organizer-workflows`: the admin tournament form composes contact-administrator selection and
  optional prizes and keeps backend validation authoritative.
- `game-detail-page-experience`: the public tournament detail presents public-safe contact
  administrator and prize information.

## Impact

- Front end only: tournament DTOs/models, `ILANClient`, tournament service, mock backend
  data/services, admin tournament create/edit surfaces, public tournament detail, placement
  display, localization, and focused contract tests.
- No back-end route or DTO change is introduced here; the back-end contract (issue 86) is
  authoritative for authorization, validation, prize limits, and privacy projection.
