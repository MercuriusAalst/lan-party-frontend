## Why

The authenticated desktop header can shrink global search until only its magnifier fits, leaving no visible input area.

## What Changes

- Preserve a usable desktop search field alongside authenticated navigation controls.
- Verify an authenticated admin can search without horizontal header overflow.

## Capabilities

### Modified Capabilities

- `site-navigation`: Global search remains usable with authenticated desktop navigation.

## Impact

- Header search sizing and its focused E2E coverage.
- No API, database, or package changes.
